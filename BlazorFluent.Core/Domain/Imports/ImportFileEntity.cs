using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Imports;

/// <summary>
/// Represents a raw uploaded file metadata record, strictly isolated per-tenant and per-project.
/// Tracks the encrypted blob reference in Azure Blob Storage, processing lifecycle, row metrics, and forensic hash.
/// </summary>
public class ImportFileEntity : TenantAuditableEntity, IProjectScopedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// ID of the project workspace to which this file import belongs.
    /// Intercepted by ProjectSecurityInterceptor and QueryFilters.Tenant.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Shared batch identifier grouping all files uploaded together in the same session.
    /// Indexed to allow querying all files in a batch by ImportId.
    /// </summary>
    public Guid ImportId { get; set; }

    /// <summary>
    /// Relative or absolute storage folder path containing all encrypted payloads for this batch.
    /// Format: tenants/{tenantId}/projects/{projectId}/imports/{year}/{month}/batch-{importId}/
    /// </summary>
    public string StorageFolder { get; set; } = string.Empty;

    /// <summary>
    /// Current execution step in the 3-stage pipeline: 'Validate', 'Stage', 'Process', or 'Completed'.
    /// </summary>
    public string StepStage { get; set; } = "None";

    /// <summary>
    /// Sanitized unique file name stored in the system.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Original file name provided by the user during upload.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Canonical URI / virtual path to the encrypted payload in Azure Blob Storage.
    /// Format: tenants/{tenantId}/projects/{projectId}/imports/{year}/{month}/{importFileId}.bin
    /// </summary>
    public string BlobUri { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type of the uploaded file.
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// Size of the raw file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Cryptographic SHA-256 hash of the unencrypted file content for tamper-detection and integrity verification.
    /// </summary>
    public string Sha256Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Format type of the imported file (Excel, Json, Xml, DelimitedText).
    /// </summary>
    public ImportFileType FileType { get; set; }

    /// <summary>
    /// Current lifecycle processing state (Uploaded, Validating, ValidationSuccess, ValidationFailed, StagingSuccess, etc.).
    /// </summary>
    public ImportStatus Status { get; set; } = ImportStatus.Uploaded;

    /// <summary>
    /// Total number of data records/rows parsed from the file.
    /// </summary>
    public int TotalRowsCount { get; set; }

    /// <summary>
    /// Number of records that successfully passed schema and invariant validation.
    /// </summary>
    public int ValidRowsCount { get; set; }

    /// <summary>
    /// Number of records that failed validation.
    /// </summary>
    public int ErrorRowsCount { get; set; }

    /// <summary>
    /// Structured JSON array containing cell-level and row-level validation errors (if validation failed).
    /// </summary>
    public string? ValidationErrorsJson { get; set; }

    /// <summary>
    /// Optional high-level error message or exception summary if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when staging or validation completed.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }

    #region ISoftDeletableEntity

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    #endregion
}
