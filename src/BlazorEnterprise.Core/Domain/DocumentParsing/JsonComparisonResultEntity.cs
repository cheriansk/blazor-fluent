using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Imports;

namespace BlazorEnterprise.Core.Domain.DocumentParsing;

/// <summary>
/// Stores the RFC 6902 JSON Patch diff and summary metrics comparing two parsed
/// <see cref="ImportFileEntity"/> documents within the same tenant and project.
/// </summary>
public class JsonComparisonResultEntity : ProjectScopedEntity, ISoftDeletableEntity, IUnversionedEntity
{
    /// <summary>
    /// Foreign key to the baseline (source) <see cref="ImportFileEntity"/>.
    /// </summary>
    public required Guid SourceFileId { get; set; }

    /// <summary>
    /// Foreign key to the target <see cref="ImportFileEntity"/> compared against the source.
    /// </summary>
    public required Guid TargetFileId { get; set; }

    /// <summary>
    /// RFC 6902 JSON Patch diff payload (<c>jsonb</c>).
    /// </summary>
    public required string DiffResultJson { get; set; }

    /// <summary>
    /// Summary counts (additions, deletions, modifications) serialized as JSON (<c>jsonb</c>).
    /// </summary>
    public required string SummaryMetricsJson { get; set; }

    /// <summary>
    /// Soft-delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// UTC timestamp when soft-deleted.
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// User identifier who soft-deleted this comparison result.
    /// </summary>
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public ImportFileEntity? SourceFile { get; set; }
    public ImportFileEntity? TargetFile { get; set; }
}
