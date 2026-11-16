using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Projection of a parsed document extraction result for the tabular review and finalization UI.
/// </summary>
public sealed record DocumentParsingReviewItemDto(
    Guid Id,
    Guid ImportFileId,
    Guid? ProjectId,
    string FileName,
    string DocumentType,
    string ExtractedDataJson,
    double ConfidenceScore,
    DocumentParsingStatusEnum Status,
    DateTime Created,
    DateTime? Updated,
    DateTime? FinalizedAtUtc,
    string? FinalizedBy);

/// <summary>
/// Service for reviewing, correcting, transitioning, and permanently finalizing parsed document results.
/// </summary>
public interface IDocumentParsingReviewService
{
    /// <summary>
    /// Lists parsed document results for the active tenant (optionally filtered by <paramref name="projectId"/>).
    /// </summary>
    Task<IReadOnlyList<DocumentParsingReviewItemDto>> GetResultsAsync(Guid? projectId = null, CancellationToken ct = default);

    /// <summary>
    /// Updates extracted JSON data. Fails closed unless <see cref="DocumentParsingStatusEnum.PendingReview"/>.
    /// </summary>
    Task<Result<bool>> UpdateExtractedDataAsync(Guid resultId, string extractedDataJson, CancellationToken ct = default);

    /// <summary>
    /// Transitions a parsed result's <see cref="DocumentParsingStatusEnum"/>. Fails closed if already <see cref="DocumentParsingStatusEnum.Finalized"/>.
    /// </summary>
    Task<Result<bool>> ChangeStatusAsync(Guid resultId, DocumentParsingStatusEnum newStatus, CancellationToken ct = default);

    /// <summary>
    /// Permanently finalizes one or more parsed results, locking them forever.
    /// </summary>
    Task<Result<int>> FinalizeBatchAsync(IReadOnlyList<Guid> resultIds, CancellationToken ct = default);
}
