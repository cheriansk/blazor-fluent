using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.DocumentParsing;

/// <summary>
/// Stores per-tenant ML.NET document parsing model configuration and extraction rules
/// for a specific document type (e.g., "PolicyCenter", "ContactManager", "ClaimCenter").
/// </summary>
public class DocumentParsingConfigEntity : TenantScopedEntity, ISoftDeletableEntity, IUnversionedEntity
{
    /// <summary>
    /// Logical document type identifier (e.g., "PolicyCenter", "ContactManager", "ClaimCenter").
    /// </summary>
    public required string DocumentType { get; set; }

    /// <summary>
    /// Relative blob storage path to the tenant-specific trained ML.NET model (.zip).
    /// </summary>
    public required string ModelBlobPath { get; set; }

    /// <summary>
    /// JSON payload defining section-table extraction rules and metadata patterns.
    /// </summary>
    public required string ParsingRulesJson { get; set; }

    /// <summary>
    /// Minimum confidence score (0.0 to 1.0) required to accept automated classification/extraction.
    /// </summary>
    public double MinConfidenceThreshold { get; set; }

    /// <summary>
    /// Soft-delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// UTC timestamp when soft-deleted.
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// User identifier who soft-deleted this configuration.
    /// </summary>
    public string? DeletedBy { get; set; }
}
