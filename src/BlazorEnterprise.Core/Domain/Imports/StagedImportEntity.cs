using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Imports;

/// <summary>
/// Universal staging table ('staging.StagedImports') storing normalized, high-density canonical JSON chunks
/// for ALL file import formats (Excel, CSV, TSV, space-delimited, XML, JSON).
/// Eliminates the need for per-file-type staging tables and database migrations.
/// </summary>
public class StagedImportEntity : TenantAuditableEntity, IProjectScopedEntity, IHardDeletableEntity
{
    /// <summary>
    /// Shared batch identifier grouping all files uploaded together in the same session.
    /// </summary>
    public required Guid ImportId { get; set; }

    /// <summary>
    /// Foreign key link to the parent ImportFileEntity.
    /// </summary>
    public required Guid ImportFileId { get; set; }

    /// <summary>
    /// ID of the project workspace.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// Logical file template type (e.g., 'TaskExcel', 'TaskCsv', 'InvoiceXml').
    /// </summary>
    public required string FileType { get; set; }

    /// <summary>
    /// Original file name uploaded by the user.
    /// </summary>
    public required string OriginalFileName { get; set; }

    /// <summary>
    /// Originating worksheet or root section name.
    /// </summary>
    public required string SheetName { get; set; }

    /// <summary>
    /// 1-based chunk index within the staged file.
    /// </summary>
    public required int ChunkIndex { get; set; }

    /// <summary>
    /// Total number of chunks generated for this file.
    /// </summary>
    public required int TotalChunks { get; set; }

    /// <summary>
    /// Number of rows contained within this JSON chunk (typically up to 1,000).
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Explicit flag indicating whether this JSON chunk has been processed and committed to live domain tables.
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Lifecycle status of the staged chunk: 'Staged', 'Processing', 'Processed', or 'Failed'.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Timestamp when Cloud API processing completed for this chunk.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>
    /// Diagnostic error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// High-density canonical JSON payload (CanonicalStagedChunkPayload) stored as PostgreSQL jsonb.
    /// </summary>
    public required string PayloadJson { get; set; }
}
