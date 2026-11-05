using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Models;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Service contract governing enterprise multi-draft scenario revisions, strictly monotonic VerNum allocation,
/// branch lineage tracing (ParentVerNum), review workflows, and atomic single-active activation cascades.
/// </summary>
public interface IRevisionService
{
    /// <summary>
    /// Gets all revisions for the specified scenario scope and feature type.
    /// </summary>
    Task<List<RevisionSummaryDto>> GetRevisionsAsync(
        TaskLevelEnum scopeLevel,
        VersionedFeatureTypeEnum featureType,
        Guid? programId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current active baseline revision for the specified scenario scope and feature type.
    /// </summary>
    Task<RevisionHeaderEntity?> GetActiveRevisionAsync(
        TaskLevelEnum scopeLevel,
        VersionedFeatureTypeEnum featureType,
        Guid? programId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new draft revision. Allocates next strictly monotonic VerNum (never reused).
    /// Copies rows from source revision preserving RelId and stamping ParentVerNum and ParentRowId.
    /// </summary>
    Task<RevisionHeaderEntity> CreateDraftAsync(
        TaskLevelEnum scopeLevel,
        VersionedFeatureTypeEnum featureType,
        string revisionName,
        Guid? sourceRevisionHeaderId = null,
        Guid? programId = null,
        Guid? projectId = null,
        bool isBatch = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a draft for formal review and assigns to designated reviewer emails.
    /// </summary>
    Task<RevisionHeaderEntity> SubmitForReviewAsync(
        Guid revisionHeaderId,
        string reviewerEmails,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a revision under review and automatically assigns it back to the draft creator.
    /// </summary>
    Task<RevisionHeaderEntity> ApproveRevisionAsync(
        Guid revisionHeaderId,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a revision under review, reverts status to Draft, notes reason, and assigns back to creator.
    /// </summary>
    Task<RevisionHeaderEntity> RejectRevisionAsync(
        Guid revisionHeaderId,
        string rejectionReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes an approved revision to Active. Supersedes previous active version and cascades
    /// cancellation to all other open Draft, ReadyForReview, and Approved revisions in the same scenario.
    /// </summary>
    Task<RevisionHeaderEntity> ActivateRevisionAsync(
        Guid revisionHeaderId,
        string? activationNotes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards an open draft or cancels a revision, permanently locking its VerNum so it is never reused.
    /// </summary>
    Task<RevisionHeaderEntity> DiscardDraftAsync(
        Guid revisionHeaderId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two revisions row-by-row matching on RelId.
    /// </summary>
    Task<RevisionComparisonDto> CompareRevisionsAsync(
        Guid leftRevisionHeaderId,
        Guid rightRevisionHeaderId,
        CancellationToken cancellationToken = default);
}
