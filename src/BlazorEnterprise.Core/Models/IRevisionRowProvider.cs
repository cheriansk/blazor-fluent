using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Models;

/// <summary>
/// Result pair returned when finalizing an ISprintVersionEntity revision.
/// </summary>
/// <param name="FinalizedBaseline">The locked baseline revision (marked Active).</param>
/// <param name="WorkingDraft">The newly auto-spawned working draft (marked Draft) for live edits.</param>
public sealed record FinalizeRevisionResult(
    RevisionHeaderEntity FinalizedBaseline,
    RevisionHeaderEntity WorkingDraft);

/// <summary>
/// Generic comparable row item consumed by revision comparison engines.
/// </summary>
public sealed class RevisionRowComparableItem
{
    public required Guid RelId { get; set; }
    public required string Title { get; set; }
    public string? EntryType { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Dictionary<string, string?> CustomProperties { get; set; } = new();
}

/// <summary>
/// Pluggable row provider contract for row-by-row revision diffs in CompareRevisionsAsync.
/// Decouples revision comparison from specific table entities.
/// </summary>
public interface IRevisionRowProvider
{
    bool CanHandle(VersionedFeatureTypeEnum featureType);

    Task<List<RevisionRowComparableItem>> GetRowsAsync(
        Guid revisionHeaderId,
        CancellationToken cancellationToken = default);
}
