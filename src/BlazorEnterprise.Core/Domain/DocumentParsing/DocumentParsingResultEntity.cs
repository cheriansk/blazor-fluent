using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Imports;

namespace BlazorEnterprise.Core.Domain.DocumentParsing;

/// <summary>
/// Stores the structured JSON output and confidence metrics produced by local ML.NET + OpenXML
/// parsing of an uploaded Word (.docx) <see cref="ImportFileEntity"/>.
/// </summary>
public class DocumentParsingResultEntity : ProjectScopedEntity, ISoftDeletableEntity, IUnversionedEntity
{
    /// <summary>
    /// Foreign key to the source <see cref="ImportFileEntity"/> that was parsed.
    /// </summary>
    public required Guid ImportFileId { get; set; }

    /// <summary>
    /// Classified document type (e.g., "PolicyCenter", "ContactManager", "ClaimCenter").
    /// </summary>
    public required string DocumentType { get; set; }

    /// <summary>
    /// Extracted metadata and section tables serialized as structured JSON (<c>jsonb</c>).
    /// </summary>
    public required string ExtractedDataJson { get; set; }

    /// <summary>
    /// Classification/extraction confidence score in the range [0.0, 1.0].
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Review and finalization lifecycle status (PendingReview, ReviewComplete, Finalized).
    /// </summary>
    public DocumentParsingStatusEnum Status { get; set; }

    /// <summary>
    /// UTC timestamp when this result was permanently finalized.
    /// </summary>
    public DateTime? FinalizedAtUtc { get; set; }

    /// <summary>
    /// User identifier who permanently finalized this result.
    /// </summary>
    public string? FinalizedBy { get; set; }

    /// <summary>
    /// Soft-delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// UTC timestamp when soft-deleted.
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// User identifier who soft-deleted this result.
    /// </summary>
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public ImportFileEntity? ImportFile { get; set; }
}
