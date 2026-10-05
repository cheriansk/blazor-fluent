using BlazorFluent.Core.Abstractions.Imports;
using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.DTOs.Imports;

/// <summary>
/// UI form request DTO for uploading a file to be imported.
/// Validated by Tier 1 UploadFileRequestValidator.
/// </summary>
public class UploadFileRequest
{
    public Guid ProjectId { get; set; }
    public ImportFileType FileType { get; set; } = ImportFileType.TaskExcel;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public Stream? ContentStream { get; set; }
}

/// <summary>
/// Represents a single file item inside a bulk upload batch.
/// </summary>
public class UploadBatchFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public ImportFileType FileType { get; set; } = ImportFileType.TaskExcel;
    public Stream? ContentStream { get; set; }
}

/// <summary>
/// Bulk upload request containing one or more files to ingest atomically under a single ImportId.
/// </summary>
public class UploadBatchRequest
{
    public Guid ProjectId { get; set; }
    public List<UploadBatchFileItem> Files { get; set; } = new();
}

/// <summary>
/// Read model representing an imported file and its staging metrics in the UI grid.
/// </summary>
public record ImportFileSummaryDto(
    Guid Id,
    Guid ImportId,
    Guid ProjectId,
    string OriginalFileName,
    string StorageFolder,
    ImportFileType FileType,
    ImportStatus Status,
    string StepStage,
    long FileSizeBytes,
    int TotalRowsCount,
    int ValidRowsCount,
    int ErrorRowsCount,
    string Sha256Checksum,
    string? ErrorMessage,
    DateTime Created,
    string? CreatedBy);

/// <summary>
/// Read model summarizing an entire batch upload session.
/// </summary>
public record ImportBatchSummaryDto(
    Guid ImportId,
    Guid ProjectId,
    string StorageFolder,
    int TotalFilesCount,
    int CompletedFilesCount,
    int FailedFilesCount,
    ImportStatus OverallStatus,
    DateTime Created,
    string? CreatedBy);

/// <summary>
/// Detailed read model including parsed validation error diagnostics.
/// </summary>
public record ImportFileDetailsDto(
    Guid Id,
    Guid ProjectId,
    string OriginalFileName,
    string BlobUri,
    ImportFileType FileType,
    ImportStatus Status,
    long FileSizeBytes,
    int TotalRowsCount,
    int ValidRowsCount,
    int ErrorRowsCount,
    string Sha256Checksum,
    string? ErrorMessage,
    IReadOnlyList<FileValidationError> ValidationErrors,
    DateTime Created,
    string? CreatedBy);
