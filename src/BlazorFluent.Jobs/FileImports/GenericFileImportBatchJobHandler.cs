using System.Globalization;
using System.Text.Json;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos;
using BlazorFluent.Core.Events.Imports;
using BlazorFluent.Core.Security;
using BlazorFluent.Core.Storage;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.FileImports.Configuration;
using BlazorFluent.Jobs.FileImports.Staging;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.FileImports;

/// <summary>
/// Generic batch worker handling FileImportBatchJobEvent.
/// Executes the Hybrid Canonical Batch Processing Pipeline:
/// 1. Orders files by manifest DependsOn dependencies.
/// 2. Stages validated rows into the universal staging.StagedImports table using ILocalBatchStagerResolver.
/// 3. Compresses staged JSON chunks with Gzip and dispatches them to the stateless Cloud API (/api/v1/transforms/process-batch).
/// 4. Enforces all-or-nothing atomicity and commits processed domain rows (including optional byte[] EncryptedPayloadBytes) to PostgreSQL.
/// </summary>
public class GenericFileImportBatchJobHandler : IBatchJobHandler<FileImportBatchJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantEncryptionService _encryptionService;
    private readonly ITenantBlobStorageService _blobStorageService;
    private readonly INotifyService _notificationService;
    private readonly IJobEventPublisher _eventPublisher;
    private readonly IFileTemplateConfigRepository? _templateRepository;
    private readonly ILocalBatchStagerResolver? _batchStagerResolver;
    private readonly IEngineClient? _engineClient;
    private readonly ILogger<GenericFileImportBatchJobHandler> _logger;

    public GenericFileImportBatchJobHandler(
        AppDbContext dbContext,
        ITenantEncryptionService encryptionService,
        ITenantBlobStorageService blobStorageService,
        INotifyService notificationService,
        IJobEventPublisher eventPublisher,
        ILogger<GenericFileImportBatchJobHandler> logger,
        IFileTemplateConfigRepository? templateRepository = null,
        ILocalBatchStagerResolver? batchStagerResolver = null,
        IEngineClient? engineClient = null)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _blobStorageService = blobStorageService;
        _notificationService = notificationService;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _templateRepository = templateRepository;
        _batchStagerResolver = batchStagerResolver;
        _engineClient = engineClient;
    }

    public async Task HandleAsync(FileImportBatchJobEvent jobEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GenericFileImportBatchJobHandler starting for ImportId={ImportId} (CorrelationId={CorrelationId})",
            jobEvent.ImportId, jobEvent.CorrelationId);

        var files = await _dbContext.ImportFiles
            .Where(f => f.TenantId == jobEvent.TenantId && f.ImportId == jobEvent.ImportId && !f.IsDeleted)
            .OrderBy(f => f.Created)
            .ToListAsync(cancellationToken);

        if (files.Count == 0)
        {
            _logger.LogWarning("No ImportFileEntity records found for batch {ImportId} and tenant {TenantId}. Aborting.", jobEvent.ImportId, jobEvent.TenantId);
            return;
        }

        // Sort files topologically by DependsOn manifest order if template configs are available
        var orderedFiles = await OrderFilesByDependenciesAsync(files, cancellationToken);

        _logger.LogInformation("Batch {ImportId} contains {Count} file(s) to process for tenant {TenantId}.", jobEvent.ImportId, orderedFiles.Count, jobEvent.TenantId);

        foreach (var file in orderedFiles)
        {
            if (file.TenantId != jobEvent.TenantId)
            {
                _logger.LogCritical("Zero-Trust Security Violation: ImportFile {FileId} belongs to tenant '{FileTenantId}' but batch job was triggered for tenant '{JobTenantId}'. Aborting batch execution immediately.",
                    file.Id, file.TenantId, jobEvent.TenantId);

                throw new InvalidOperationException($"Zero-Trust Security Violation: Cross-tenant data access detected for file {file.Id}. Execution halted.");
            }

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["FileType"] = file.FileType.ToString(),
                ["FileImportId"] = file.Id,
                ["BatchId"] = file.ImportId
            });

            await ProcessSingleFileAsync(file, jobEvent, cancellationToken);
        }

        _logger.LogInformation("Finished processing all {Count} files for Batch {ImportId}.", orderedFiles.Count, jobEvent.ImportId);
    }

    private async Task<List<ImportFileEntity>> OrderFilesByDependenciesAsync(List<ImportFileEntity> files, CancellationToken ct)
    {
        if (_templateRepository == null || files.Count <= 1)
        {
            return files;
        }

        var result = new List<ImportFileEntity>(files.Count);
        var visited = new HashSet<Guid>();
        var visiting = new HashSet<Guid>();

        async Task Visit(ImportFileEntity file)
        {
            if (visited.Contains(file.Id)) return;
            if (!visiting.Add(file.Id)) return; // Guard against circular dependency in config

            var template = await _templateRepository.GetConfigAsync(file.FileType.ToString(), ct);
            if (template?.DependsOn is { Count: > 0 })
            {
                foreach (var depType in template.DependsOn)
                {
                    var predecessors = files.Where(f =>
                        string.Equals(f.FileType.ToString(), depType, StringComparison.OrdinalIgnoreCase));
                    foreach (var pred in predecessors)
                    {
                        await Visit(pred);
                    }
                }
            }

            visiting.Remove(file.Id);
            visited.Add(file.Id);
            result.Add(file);
        }

        foreach (var file in files)
        {
            await Visit(file);
        }

        return result;
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
            var templateConfig = _templateRepository is null ? null : await _templateRepository.GetConfigAsync(file.FileType.ToString(), ct);
            if (templateConfig == null || _batchStagerResolver == null || _engineClient == null)
            {
                var errorMsg = $"No declarative FileTemplateConfig or Canonical Batch Stager registered for file type '{file.FileType}'.";
                _logger.LogError(errorMsg);

                file.Status = ImportStatusEnum.ValidationFailed;
                file.StepStage = "ValidationFailed";
                file.ErrorMessage = errorMsg;
                file.ValidationErrorsJson = JsonSerializer.Serialize(new[]
                {
                    new FileValidationError("System", 0, "FileType", errorMsg, file.FileType.ToString())
                });
                file.ProcessedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);

                await SendNotificationAsync(file, "Import Failed", errorMsg, NotificationSeverityEnum.Error, ct);
                return;
            }

            await ProcessHybridCanonicalPipelineAsync(file, templateConfig, jobEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing file '{FileName}' ({FileId})", file.OriginalFileName, file.Id);

            file.Status = ImportStatusEnum.ProcessingFailed;
            file.StepStage = "Failed";
            file.ErrorMessage = $"Unhandled pipeline error: {ex.Message}";
            file.ProcessedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            await SendNotificationAsync(
                file,
                title: "File Processing Error",
                message: $"File '{file.OriginalFileName}' failed due to an unexpected error: {ex.Message}",
                severity: NotificationSeverityEnum.Error,
                ct);
        }
    }

    private async Task ProcessHybridCanonicalPipelineAsync(
        ImportFileEntity file,
        FileTemplateConfig templateConfig,
        FileImportBatchJobEvent jobEvent,
        CancellationToken ct)
    {
        var canonicalStager = _batchStagerResolver!.ResolveStager(templateConfig);

        // 1. Download & Decrypt Stream Locally
        using var encryptedStream = await _blobStorageService.DownloadBlobAsync(file.BlobUri, ct);
        using var decryptedStream = await _encryptionService.DecryptStreamAsync(encryptedStream, file.TenantId.ToString(), ct);

        // 2. Local Schema Validation & Universal JSON Staging (staging.StagedImports)
        _logger.LogInformation(
            "Executing Hybrid Canonical Staging (Strategy='{Strategy}') for '{FileName}'",
            canonicalStager.StrategyName, file.OriginalFileName);

        var stageOutcome = await canonicalStager.ValidateAndStageCanonicalJsonAsync(file, decryptedStream, templateConfig, ct);
        if (!stageOutcome.IsSuccess)
        {
            var errorSummary = stageOutcome.Error ?? "Local schema validation failed.";
            _logger.LogWarning("Hybrid Canonical Staging failed for '{FileName}': {Error}", file.OriginalFileName, errorSummary);

            file.Status = ImportStatusEnum.ValidationFailed;
            file.StepStage = "ValidationFailed";
            file.ErrorMessage = errorSummary;
            file.ErrorRowsCount = 1;
            file.ProcessedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            await _eventPublisher.PublishAsync(new FileStagingCompletedJobEvent(
                file.Id,
                IsSuccess: false,
                StagedCount: 0,
                ErrorMessage: errorSummary,
                TenantId: file.TenantId,
                CorrelationId: jobEvent.CorrelationId,
                SenderOrigin: jobEvent.SenderOrigin,
                SenderUserId: jobEvent.SenderUserId,
                SenderUserEmail: jobEvent.SenderUserEmail,
                ParentExecutionId: jobEvent.EventId), ct);

            await SendNotificationAsync(
                file,
                title: "File Validation / Staging Failed",
                message: $"File '{file.OriginalFileName}' failed validation: {errorSummary}",
                severity: NotificationSeverityEnum.Warning,
                ct);

            return;
        }

        file.Status = ImportStatusEnum.StagingSuccess;
        file.StepStage = "Staged";
        file.TotalRowsCount = stageOutcome.Value;
        file.ValidRowsCount = stageOutcome.Value;
        file.ErrorRowsCount = 0;
        await _dbContext.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(new FileStagingCompletedJobEvent(
            file.Id,
            IsSuccess: true,
            StagedCount: stageOutcome.Value,
            ErrorMessage: null,
            TenantId: file.TenantId,
            CorrelationId: jobEvent.CorrelationId,
            SenderOrigin: jobEvent.SenderOrigin,
            SenderUserId: jobEvent.SenderUserId,
            SenderUserEmail: jobEvent.SenderUserEmail,
            ParentExecutionId: jobEvent.EventId), ct);

        // 3. Dispatch Unprocessed Staged Chunks to Stateless Cloud API (All-or-Nothing Atomicity)
        var stagedChunks = await _dbContext.StagedImports
            .Where(s => s.TenantId == file.TenantId && s.ImportFileId == file.Id && !s.IsProcessed)
            .ToListAsync(ct);

        var sheetOrder = (templateConfig.Sheets ?? [])
            .Select((s, idx) => new { s.Name, idx })
            .ToDictionary(x => x.Name, x => x.idx, StringComparer.OrdinalIgnoreCase);

        var orderedChunks = stagedChunks
            .OrderBy(s => sheetOrder.TryGetValue(s.SheetName, out var idx) ? idx : int.MaxValue)
            .ThenBy(s => s.ChunkIndex)
            .ToList();

        var allProcessedRows = new List<ProcessedDomainRowDto>();

        foreach (var chunk in orderedChunks)
        {
            chunk.Status = "Processing";
            var compressedBytes = PayloadEncryptionUtility.CompressGzip(chunk.PayloadJson);

            var batchReq = new ProcessBatchReqDto(
                ImportId: file.ImportId,
                ImportFileId: file.Id,
                FileType: chunk.FileType,
                ChunkIndex: chunk.ChunkIndex,
                TotalChunks: chunk.TotalChunks,
                IsLastChunk: chunk.ChunkIndex == chunk.TotalChunks,
                HeaderMap: new Dictionary<string, string>(),
                Rows: [],
                CompressedChunkBytes: compressedBytes);

            var engineResult = await _engineClient!.ProcessStagedBatchAsync(batchReq, ct);

            if (!engineResult.IsSuccess || engineResult.Data == null || !engineResult.Data.IsSuccess)
            {
                var cloudErrors = engineResult.Data?.Errors ?? [];
                var firstError = cloudErrors.FirstOrDefault()?.ErrorMessage
                    ?? engineResult.Error
                    ?? engineResult.Data?.ErrorMessage
                    ?? "Cloud batch processing rejected the staged chunk.";

                // All-or-nothing rollback: mark all chunks for this file as Failed and commit 0 domain rows
                foreach (var c in orderedChunks)
                {
                    c.IsProcessed = false;
                    c.Status = "Failed";
                    c.ErrorMessage = firstError;
                }

                file.Status = ImportStatusEnum.ProcessingFailed;
                file.StepStage = "ProcessingFailed";
                file.ErrorMessage = firstError;
                file.ErrorRowsCount = cloudErrors.Count;
                if (cloudErrors.Count > 0)
                {
                    file.ValidationErrorsJson = JsonDefaults.ToJson(cloudErrors);
                }
                file.ProcessedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);

                await _eventPublisher.PublishAsync(new FileProcessingCompletedJobEvent(
                    file.Id,
                    IsSuccess: false,
                    ProcessedCount: 0,
                    ErrorMessage: firstError,
                    TenantId: file.TenantId,
                    CorrelationId: jobEvent.CorrelationId,
                    SenderOrigin: jobEvent.SenderOrigin,
                    SenderUserId: jobEvent.SenderUserId,
                    SenderUserEmail: jobEvent.SenderUserEmail,
                    ParentExecutionId: jobEvent.EventId), ct);

                await SendNotificationAsync(
                    file,
                    title: "Domain Processing Failed",
                    message: $"File '{file.OriginalFileName}' failed during cloud batch processing: {firstError}",
                    severity: NotificationSeverityEnum.Error,
                    ct);

                return;
            }

            allProcessedRows.AddRange(engineResult.Data.ProcessedRows);
        }

        // 4. Atomic Commit of All Processed Domain Rows & StagedImport Flags
        var uploaderEmail = string.IsNullOrWhiteSpace(file.CreatedBy) ? "system@local" : file.CreatedBy;

        foreach (var row in allProcessedRows)
        {
            if (string.Equals(row.TargetEntityName, "TaskEntity", StringComparison.OrdinalIgnoreCase))
            {
                row.DomainProperties.TryGetValue("Title", out var title);
                row.DomainProperties.TryGetValue("Description", out var description);
                row.DomainProperties.TryGetValue("Status", out var statusStr);
                row.DomainProperties.TryGetValue("Priority", out var priorityStr);
                row.DomainProperties.TryGetValue("DueDateUtc", out var dueDateStr);

                var status = statusStr.ToEnum(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.Open);
                var priority = priorityStr.ToEnum(TaskPriorityEnum.Medium);

                DateTime dueDateUtc = DateTime.UtcNow.AddDays(7);
                if (!string.IsNullOrWhiteSpace(dueDateStr) &&
                    DateTime.TryParse(dueDateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDue))
                {
                    dueDateUtc = parsedDue;
                }

                var taskEntity = new TaskEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = file.TenantId,
                    ProjectId = file.ProjectId,
                    Level = TaskLevelEnum.Project,
                    Title = string.IsNullOrWhiteSpace(title) ? "[ENCRYPTED]" : title,
                    Description = description,
                    Status = status,
                    Priority = priority,
                    StartDate = DateTime.UtcNow,
                    DueDate = dueDateUtc,
                    ReporterEmail = uploaderEmail,
                    SourceRowId = row.SourceRowId,
                    IsEncryptedAtRest = row.IsEncryptedAtRest,
                    EncryptedPayloadBytes = row.EncryptedPayloadBytes
                };

                _dbContext.Tasks.Add(taskEntity);
            }
        }

        var nowUtc = DateTime.UtcNow;
        foreach (var chunk in orderedChunks)
        {
            chunk.IsProcessed = true;
            chunk.Status = "Processed";
            chunk.ProcessedAtUtc = nowUtc;
            chunk.ErrorMessage = null;
        }

        file.Status = ImportStatusEnum.Completed;
        file.StepStage = "Completed";
        file.ValidRowsCount = allProcessedRows.Count;
        file.ProcessedAtUtc = nowUtc;

        await _dbContext.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(new FileProcessingCompletedJobEvent(
            file.Id,
            IsSuccess: true,
            ProcessedCount: allProcessedRows.Count,
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
            message: $"File '{file.OriginalFileName}' successfully processed. {allProcessedRows.Count} records committed to project.",
            severity: NotificationSeverityEnum.Success,
            ct);
    }

    private async Task SendNotificationAsync(
        ImportFileEntity file,
        string title,
        string message,
        NotificationSeverityEnum severity,
        CancellationToken ct)
    {
        try
        {
            var uploader = file.CreatedBy;
            if (string.IsNullOrWhiteSpace(uploader)) return;

            var request = new SendNotificationReqDto
            {
                Category = NotificationCategoryEnum.Generic,
                Severity = severity,
                Title = title,
                Message = message,
                ProjectId = file.ProjectId,
                UserId = uploader,
                LinkUrl = "/imports",
                Channels = NotificationChannelsEnum.InApp
            };

            await _notificationService.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification for file {ImportFileId}", file.Id);
        }
    }
}
