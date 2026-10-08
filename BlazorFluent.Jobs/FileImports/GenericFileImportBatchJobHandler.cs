using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Imports;

using BlazorFluent.Core.Events.Imports;
using BlazorFluent.Core.Security;
using BlazorFluent.Core.Storage;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.FileImports.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BlazorFluent.Core.Dtos;

namespace BlazorFluent.Jobs.FileImports;

/// <summary>
/// Generic batch worker handling FileImportBatchJobEvent.
/// Processes all files in a batch sequentially through two discrete phases:
/// 1. Validate & Stage (via auto-discovered IFileStager)
/// 2. Domain Promotion (via auto-discovered IFileProcessor)
/// </summary>
public class GenericFileImportBatchJobHandler : IBatchJobHandler<FileImportBatchJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantEncryptionService _encryptionService;
    private readonly ITenantBlobStorageService _blobStorageService;
    private readonly INotificationService _notificationService;
    private readonly IJobEventPublisher _eventPublisher;
    private readonly IEnumerable<IFileStager> _stagers;
    private readonly IEnumerable<IFileProcessor> _processors;
    private readonly ILogger<GenericFileImportBatchJobHandler> _logger;

    public GenericFileImportBatchJobHandler(
        AppDbContext dbContext,
        ITenantEncryptionService encryptionService,
        ITenantBlobStorageService blobStorageService,
        INotificationService notificationService,
        IJobEventPublisher eventPublisher,
        IEnumerable<IFileStager> stagers,
        IEnumerable<IFileProcessor> processors,
        ILogger<GenericFileImportBatchJobHandler> logger)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _blobStorageService = blobStorageService;
        _notificationService = notificationService;
        _eventPublisher = eventPublisher;
        _stagers = stagers;
        _processors = processors;
        _logger = logger;
    }

    public async Task HandleAsync(FileImportBatchJobEvent jobEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GenericFileImportBatchJobHandler starting for ImportId={ImportId} (CorrelationId={CorrelationId})",
            jobEvent.ImportId, jobEvent.CorrelationId);

        var files = await _dbContext.ImportFiles
            .Where(f => f.ImportId == jobEvent.ImportId && !f.IsDeleted)
            .OrderBy(f => f.Created)
            .ToListAsync(cancellationToken);

        if (files.Count == 0)
        {
            _logger.LogWarning("No ImportFileEntity records found for batch {ImportId}. Aborting.", jobEvent.ImportId);
            return;
        }

        _logger.LogInformation("Batch {ImportId} contains {Count} file(s) to process.", jobEvent.ImportId, files.Count);

        foreach (var file in files)
        {
            await ProcessSingleFileAsync(file, jobEvent, cancellationToken);
        }

        _logger.LogInformation("Finished processing all {Count} files for Batch {ImportId}.", files.Count, jobEvent.ImportId);
    }

    private async Task ProcessSingleFileAsync(
        ImportFileEntity file,
        FileImportBatchJobEvent jobEvent,
        CancellationToken ct)
    {
        _logger.LogInformation("--- Starting import execution for file '{FileName}' ({FileId}, Type={FileType}) in Batch {ImportId} ---",
            file.OriginalFileName, file.Id, file.FileType, file.ImportId);

        try
        {
            // ─── 1. RESOLVE STAGER AND PROCESSOR ──────────────────────────────────
            var stager = _stagers.FirstOrDefault(s => s.CanHandle(file.FileType));
            var processor = _processors.FirstOrDefault(p => p.CanHandle(file.FileType));

            if (stager == null || processor == null)
            {
                var errorMsg = $"No handler registered for file type '{file.FileType}'. Staging or Processor missing.";
                _logger.LogError(errorMsg);

                file.Status = ImportStatus.ValidationFailed;
                file.StepStage = "ValidationFailed";
                file.ErrorMessage = errorMsg;
                file.ValidationErrorsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new BlazorFluent.Core.Abstractions.Imports.FileValidationError("System", 0, "FileType", errorMsg, file.FileType.ToString())
                });
                file.ProcessedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);

                await SendNotificationAsync(file, "Import Failed", errorMsg, NotificationSeverity.Error, ct);
                return;
            }

            // ─── 2. DOWNLOAD & DECRYPT PAYLOAD ────────────────────────────────────
            using var encryptedStream = await _blobStorageService.DownloadBlobAsync(file.BlobUri, ct);
            using var decryptedStream = await _encryptionService.DecryptStreamAsync(encryptedStream, file.TenantId, ct);

            // ─── 3. EXECUTE VALIDATE & STAGE ──────────────────────────────────────
            _logger.LogInformation("Executing Validate & Stage for '{FileName}'", file.OriginalFileName);
            var stageResult = await stager.ValidateAndStageAsync(file, decryptedStream, ct);

            if (!stageResult.IsSuccess)
            {
                _logger.LogWarning("Validate & Stage failed for '{FileName}': {Error}", file.OriginalFileName, stageResult.Error);

                await _eventPublisher.PublishAsync(new FileStagingCompletedJobEvent(
                    file.Id,
                    IsSuccess: false,
                    StagedCount: 0,
                    ErrorMessage: stageResult.Error,
                    TenantId: file.TenantId,
                    CorrelationId: jobEvent.CorrelationId,
                    SenderOrigin: jobEvent.SenderOrigin,
                    SenderUserId: jobEvent.SenderUserId,
                    SenderUserEmail: jobEvent.SenderUserEmail,
                    ParentExecutionId: jobEvent.EventId), ct);

                await SendNotificationAsync(
                    file,
                    title: "File Validation / Staging Failed",
                    message: $"File '{file.OriginalFileName}' failed: {stageResult.Error}",
                    severity: NotificationSeverity.Warning,
                    ct);

                return;
            }

            await _eventPublisher.PublishAsync(new FileStagingCompletedJobEvent(
                file.Id,
                IsSuccess: true,
                StagedCount: stageResult.Value,
                ErrorMessage: null,
                TenantId: file.TenantId,
                CorrelationId: jobEvent.CorrelationId,
                SenderOrigin: jobEvent.SenderOrigin,
                SenderUserId: jobEvent.SenderUserId,
                SenderUserEmail: jobEvent.SenderUserEmail,
                ParentExecutionId: jobEvent.EventId), ct);

            // ─── 4. EXECUTE DOMAIN PROMOTION ──────────────────────────────────────
            _logger.LogInformation("Executing Domain Promotion (Process) for '{FileName}'", file.OriginalFileName);
            var processResult = await processor.ProcessAsync(file, ct);

            if (!processResult.IsSuccess)
            {
                _logger.LogError("Domain promotion failed for '{FileName}': {Error}", file.OriginalFileName, processResult.Error);

                await _eventPublisher.PublishAsync(new FileProcessingCompletedJobEvent(
                    file.Id,
                    IsSuccess: false,
                    ProcessedCount: 0,
                    ErrorMessage: processResult.Error,
                    TenantId: file.TenantId,
                    CorrelationId: jobEvent.CorrelationId,
                    SenderOrigin: jobEvent.SenderOrigin,
                    SenderUserId: jobEvent.SenderUserId,
                    SenderUserEmail: jobEvent.SenderUserEmail,
                    ParentExecutionId: jobEvent.EventId), ct);

                await SendNotificationAsync(
                    file,
                    title: "Domain Processing Failed",
                    message: $"File '{file.OriginalFileName}' failed during live domain commit: {processResult.Error}",
                    severity: NotificationSeverity.Error,
                    ct);

                return;
            }

            // ─── 5. SUCCESS ───────────────────────────────────────────────────────
            await _eventPublisher.PublishAsync(new FileProcessingCompletedJobEvent(
                file.Id,
                IsSuccess: true,
                ProcessedCount: processResult.Value,
                ErrorMessage: null,
                TenantId: file.TenantId,
                CorrelationId: jobEvent.CorrelationId,
                SenderOrigin: jobEvent.SenderOrigin,
                SenderUserId: jobEvent.SenderUserId,
                SenderUserEmail: jobEvent.SenderUserEmail,
                ParentExecutionId: jobEvent.EventId), ct);

            await SendNotificationAsync(
                file,
                title: "File Import Completed",
                message: $"File '{file.OriginalFileName}' successfully processed. {processResult.Value} records committed to project.",
                severity: NotificationSeverity.Success,
                ct);

            _logger.LogInformation("Successfully completed ingestion and domain promotion for '{FileName}' ({Count} records).",
                file.OriginalFileName, processResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing file '{FileName}' ({FileId})", file.OriginalFileName, file.Id);

            file.Status = ImportStatus.ProcessingFailed;
            file.StepStage = "Failed";
            file.ErrorMessage = $"Unhandled pipeline error: {ex.Message}";
            file.ProcessedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            await SendNotificationAsync(
                file,
                title: "File Processing Error",
                message: $"File '{file.OriginalFileName}' failed due to an unexpected error: {ex.Message}",
                severity: NotificationSeverity.Error,
                ct);
        }
    }

    private async Task SendNotificationAsync(
        ImportFileEntity file,
        string title,
        string message,
        NotificationSeverity severity,
        CancellationToken ct)
    {
        try
        {
            var uploader = file.CreatedBy;
            if (string.IsNullOrWhiteSpace(uploader)) return;

            var request = new SendNotificationReqDto
            {
                Category = NotificationCategory.Generic,
                Severity = severity,
                Title = title,
                Message = message,
                ProjectId = file.ProjectId,
                UserId = uploader,
                LinkUrl = "/imports",
                Channels = NotificationChannels.InApp
            };

            await _notificationService.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification for file {ImportFileId}", file.Id);
        }
    }
}
