using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Events.Imports;

/// <summary>
/// Dispatched immediately after a raw file is uploaded, encrypted, and stored in blob storage.
/// Triggers the background BatchJobQueueListener to initiate validation and staging.
/// </summary>
public record FileImportedJobEvent(
    Guid ImportFileId,
    ImportFileType FileType,
    Guid ProjectId,
    string? TenantId = null,
    string? CorrelationId = null,
    Guid? ParentExecutionId = null)
    : BaseJobEvent("FileImportModule", TenantId, CorrelationId, ParentExecutionId);

/// <summary>
/// Dispatched by the file validation worker with schema and invariant validation outcomes.
/// </summary>
public record FileValidationResultJobEvent(
    Guid ImportFileId,
    bool IsSuccess,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    string? TenantId = null,
    string? CorrelationId = null,
    Guid? ParentExecutionId = null)
    : BaseJobEvent("FileValidationEngine", TenantId, CorrelationId, ParentExecutionId);

/// <summary>
/// Dispatched after records are written into staging tables or when staging fails.
/// </summary>
public record FileStagingCompletedJobEvent(
    Guid ImportFileId,
    bool IsSuccess,
    int StagedCount,
    string? ErrorMessage = null,
    string? TenantId = null,
    string? CorrelationId = null,
    Guid? ParentExecutionId = null)
    : BaseJobEvent("FileStagingWorker", TenantId, CorrelationId, ParentExecutionId);

/// <summary>
/// Dispatched immediately after an entire batch of files is uploaded and encrypted into a batch folder.
/// Triggers GenericFileImportBatchJobHandler to process each file sequentially.
/// </summary>
public record FileImportBatchJobEvent(
    Guid ImportId,
    Guid ProjectId,
    string? TenantId = null,
    string? CorrelationId = null,
    Guid? ParentExecutionId = null)
    : BaseJobEvent("FileImportBatchModule", TenantId, CorrelationId, ParentExecutionId);

/// <summary>
/// Dispatched after staged records are committed to live domain tables.
/// </summary>
public record FileProcessingCompletedJobEvent(
    Guid ImportFileId,
    bool IsSuccess,
    int ProcessedCount,
    string? ErrorMessage = null,
    string? TenantId = null,
    string? CorrelationId = null,
    Guid? ParentExecutionId = null)
    : BaseJobEvent("FileDomainProcessor", TenantId, CorrelationId, ParentExecutionId);
