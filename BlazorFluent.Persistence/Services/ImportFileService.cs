using System.Text.Json;
using BlazorFluent.Core.Abstractions.Imports;
using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.DTOs.Imports;
using BlazorFluent.Core.Events.Imports;
using BlazorFluent.Core.Security;
using BlazorFluent.Core.Storage;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Domain service coordinating multi-tenant client-side encryption, blob persistence,
/// database tracking, and asynchronous event scheduling for file imports.
/// </summary>
public class ImportFileService : IImportFileService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantEncryptionService _encryptionService;
    private readonly ITenantBlobStorageService _blobStorageService;
    private readonly IJobEventPublisher _eventPublisher;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ImportFileService> _logger;

    public ImportFileService(
        AppDbContext context,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ITenantEncryptionService encryptionService,
        ITenantBlobStorageService blobStorageService,
        IJobEventPublisher eventPublisher,
        INotificationService notificationService,
        ILogger<ImportFileService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _encryptionService = encryptionService;
        _blobStorageService = blobStorageService;
        _eventPublisher = eventPublisher;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<Guid>> UploadBatchAndQueueAsync(UploadBatchRequest request, CancellationToken ct = default)
    {
        var uploadedBlobPaths = new List<string>();

        try
        {
            var tenantId = _tenantContext.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return Result<Guid>.Failure("No active tenant context found for file upload.");
            }

            if (request.Files.Count == 0)
            {
                return Result<Guid>.Failure("At least one file must be selected for upload.");
            }

            var importId = Guid.CreateVersion7();
            var batchFolder = _blobStorageService.BuildBatchFolderPath(tenantId, request.ProjectId, importId);
            var importFiles = new List<ImportFileEntity>(request.Files.Count);

            // 1. Process and upload each file in the batch into the batch folder
            foreach (var item in request.Files)
            {
                if (item.ContentStream == null)
                {
                    throw new InvalidOperationException($"Content stream is missing for file '{item.FileName}'.");
                }

                var fileId = Guid.CreateVersion7();
                var extension = Path.GetExtension(item.FileName);
                var sanitizedFileName = $"{fileId:N}{extension}";

                // Ensure stream is at position 0 before hashing and encrypting
                Stream streamToProcess = item.ContentStream;
                MemoryStream? bufferStream = null;
                if (!streamToProcess.CanSeek)
                {
                    bufferStream = new MemoryStream();
                    await streamToProcess.CopyToAsync(bufferStream, ct);
                    bufferStream.Position = 0;
                    streamToProcess = bufferStream;
                }
                else
                {
                    streamToProcess.Position = 0;
                }

                // Compute SHA-256 on raw stream
                var checksum = await _encryptionService.ComputeSha256ChecksumAsync(streamToProcess, ct);

                if (streamToProcess.CanSeek)
                {
                    streamToProcess.Position = 0;
                }

                // Client-side AES-256-GCM encryption with tenant-derived key
                using var encryptedStream = await _encryptionService.EncryptStreamAsync(streamToProcess, tenantId, ct);
                bufferStream?.Dispose();

                // Store in batch folder: tenants/{tenantId}/projects/{projectId}/imports/{year}/{month}/batch-{importId}/{fileId}.bin
                var itemStoragePath = _blobStorageService.BuildBatchItemStoragePath(tenantId, request.ProjectId, importId, fileId, extension);
                var blobUri = await _blobStorageService.UploadBlobAsync(itemStoragePath, encryptedStream, "application/octet-stream", ct);
                uploadedBlobPaths.Add(blobUri);

                importFiles.Add(new ImportFileEntity
                {
                    Id = fileId,
                    ImportId = importId,
                    TenantId = tenantId,
                    ProjectId = request.ProjectId,
                    StorageFolder = batchFolder,
                    StepStage = "Uploaded",
                    FileName = sanitizedFileName,
                    OriginalFileName = item.FileName,
                    BlobUri = blobUri,
                    ContentType = item.ContentType,
                    FileSizeBytes = item.FileSizeBytes,
                    Sha256Checksum = checksum,
                    FileType = item.FileType,
                    Status = ImportStatus.Uploaded,
                    TotalRowsCount = 0,
                    ValidRowsCount = 0,
                    ErrorRowsCount = 0
                });
            }

            // 2. Atomically persist all ImportFileEntity records in PostgreSQL
            _context.ImportFiles.AddRange(importFiles);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Batch {ImportId} with {FileCount} file(s) uploaded and encrypted for Tenant {TenantId}, Project {ProjectId}",
                importId, importFiles.Count, tenantId, request.ProjectId);

            // 3. Enqueue generic batch event
            var batchEvent = new FileImportBatchJobEvent(
                ImportId: importId,
                ProjectId: request.ProjectId,
                TenantId: tenantId,
                SenderOrigin: "ImportFileService.UploadBatchAsync",
                SenderUserId: _currentUser.UserId,
                SenderUserEmail: _currentUser.Email);

            await _eventPublisher.PublishAsync(batchEvent, ct);

            // 4. Send in-app notification to the uploader
            await SendNotificationAsync(
                title: "Batch Upload Succeeded",
                message: $"{importFiles.Count} file(s) uploaded and encrypted in batch '{importId:N}'. Queued for background validation and staging.",
                severity: NotificationSeverity.Info,
                projectId: request.ProjectId,
                ct: ct);

            return Result<Guid>.Success(importId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload batch for project {ProjectId}. Rolling back any uploaded blobs.", request.ProjectId);

            // Rollback: clean up any blobs uploaded during this failed batch
            foreach (var blobPath in uploadedBlobPaths)
            {
                try
                {
                    await _blobStorageService.DeleteBlobAsync(blobPath, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to delete blob '{BlobPath}' during batch rollback.", blobPath);
                }
            }

            await SendNotificationAsync(
                title: "Batch Upload Failed",
                message: $"Failed to upload batch files: {ex.Message}",
                severity: NotificationSeverity.Error,
                projectId: request.ProjectId,
                ct: ct);

            return Result<Guid>.Failure($"Failed to process and store batch: {ex.Message}");
        }
    }

    public async Task<Result<Guid>> UploadAndQueueAsync(UploadFileRequest request, CancellationToken ct = default)
    {
        var batchRequest = new UploadBatchRequest
        {
            ProjectId = request.ProjectId,
            Files = new List<UploadBatchFileItem>
            {
                new()
                {
                    FileName = request.FileName,
                    ContentType = request.ContentType,
                    FileSizeBytes = request.FileSizeBytes,
                    FileType = request.FileType,
                    ContentStream = request.ContentStream
                }
            }
        };

        return await UploadBatchAndQueueAsync(batchRequest, ct);
    }

    public async Task<IReadOnlyList<ImportFileSummaryDto>> GetImportHistoryAsync(Guid projectId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        return await _context.ImportFiles
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.ProjectId == projectId)
            .OrderByDescending(f => f.Created)
            .Select(f => new ImportFileSummaryDto(
                f.Id,
                f.ImportId,
                f.ProjectId,
                f.OriginalFileName,
                f.StorageFolder,
                f.FileType,
                f.Status,
                f.StepStage,
                f.FileSizeBytes,
                f.TotalRowsCount,
                f.ValidRowsCount,
                f.ErrorRowsCount,
                f.Sha256Checksum,
                f.ErrorMessage,
                f.Created,
                f.CreatedBy))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ImportFileSummaryDto>> GetImportFilesByBatchAsync(Guid projectId, Guid importId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        return await _context.ImportFiles
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.ProjectId == projectId && f.ImportId == importId)
            .OrderBy(f => f.Created)
            .Select(f => new ImportFileSummaryDto(
                f.Id,
                f.ImportId,
                f.ProjectId,
                f.OriginalFileName,
                f.StorageFolder,
                f.FileType,
                f.Status,
                f.StepStage,
                f.FileSizeBytes,
                f.TotalRowsCount,
                f.ValidRowsCount,
                f.ErrorRowsCount,
                f.Sha256Checksum,
                f.ErrorMessage,
                f.Created,
                f.CreatedBy))
            .ToListAsync(ct);
    }

    public async Task<ImportFileDetailsDto?> GetImportFileDetailsAsync(Guid projectId, Guid importFileId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var file = await _context.ImportFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.ProjectId == projectId && f.Id == importFileId, ct);

        if (file == null) return null;

        var errors = new List<FileValidationError>();
        if (!string.IsNullOrWhiteSpace(file.ValidationErrorsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<FileValidationError>>(file.ValidationErrorsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) errors.AddRange(parsed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize ValidationErrorsJson for file {ImportFileId}", importFileId);
            }
        }

        return new ImportFileDetailsDto(
            file.Id,
            file.ProjectId,
            file.OriginalFileName,
            file.BlobUri,
            file.FileType,
            file.Status,
            file.FileSizeBytes,
            file.TotalRowsCount,
            file.ValidRowsCount,
            file.ErrorRowsCount,
            file.Sha256Checksum,
            file.ErrorMessage,
            errors,
            file.Created,
            file.CreatedBy);
    }

    public async Task<Result<bool>> DeleteImportFileAsync(Guid projectId, Guid importFileId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var file = await _context.ImportFiles.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.ProjectId == projectId && f.Id == importFileId, ct);
        if (file == null)
        {
            return Result<bool>.Failure("Import file record not found.");
        }

        file.IsDeleted = true;
        file.DeletedAtUtc = DateTime.UtcNow;
        file.DeletedBy = _currentUser.Email ?? _currentUser.UserId;

        // Clean up staged tasks for this file directly via SQL without materializing entities into memory
        await _context.StagedTasks
            .Where(s => s.TenantId == tenantId && s.ProjectId == projectId && s.ImportFileId == importFileId)
            .ExecuteDeleteAsync(ct);

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private async Task SendNotificationAsync(string title, string message, NotificationSeverity severity, Guid projectId, CancellationToken ct)
    {
        try
        {
            var userId = _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(userId)) return;

            var request = new SendNotificationRequest
            {
                Category = NotificationCategory.Generic,
                Severity = severity,
                Title = title,
                Message = message,
                ProjectId = projectId,
                UserId = userId,
                LinkUrl = "/imports",
                Channels = NotificationChannels.InApp
            };

            await _notificationService.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send import notification to user {UserId}", _currentUser.UserId);
        }
    }
}
