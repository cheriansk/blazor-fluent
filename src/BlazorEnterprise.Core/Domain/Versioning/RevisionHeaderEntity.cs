using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Versioning;

/// <summary>
/// Root header entity managing multi-draft scenario revisions across Tenant, Program, and Project levels.
/// Strictly monotonic, permanently locked VerNum per scenario scope.
/// Allows multiple concurrent drafts/reviews while enforcing strictly one Active revision at a time.
/// Stores soft relations (ParentVerNum and ParentRevisionHeaderId) to track draft branch lineage.
/// </summary>
public class RevisionHeaderEntity : TenantAuditableEntity, ISoftDeletableEntity, IUnversionedEntity
{
    /// <summary>
    /// Scope level of this revision scenario (Tenant, Program, Project).
    /// </summary>
    public TaskLevelEnum ScopeLevel { get; set; }

    /// <summary>
    /// Governing program ID if scope is Program or Project.
    /// </summary>
    public Guid? ProgramId { get; set; }

    /// <summary>
    /// Governing project ID if scope is Project.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// The business feature area governed by this revision (ProgramCalendar, ProjectCalendar, SprintPlan, etc.).
    /// </summary>
    public VersionedFeatureTypeEnum FeatureType { get; set; }

    /// <summary>
    /// Strictly monotonic, permanently locked scenario revision number (1, 2, 3...).
    /// NEVER reused within the same (TenantId, ProgramId, ProjectId, FeatureType) scenario even if draft/cancelled.
    /// </summary>
    public int VerNum { get; set; }

    /// <summary>
    /// Soft relation: The VerNum of the parent revision from which this draft was cloned.
    /// Sentinel value -1 indicates the root baseline version (no parent revision).
    /// </summary>
    public int ParentVerNum { get; set; } = -1;

    /// <summary>
    /// Soft relation: The RevisionHeader ID from which this draft was cloned.
    /// Sentinel value Guid.Empty indicates the root baseline version (no parent revision).
    /// </summary>
    public Guid ParentRevisionHeaderId { get; set; } = Guid.Empty;

    /// <summary>
    /// User-defined or batch-generated human-readable scenario revision name.
    /// E.g. "Q3 Initial Baseline", "Alice Budget Draft v2", "Batch Import - 2026-11-05 14:30".
    /// </summary>
    public required string RevisionName { get; set; }

    /// <summary>
    /// Lifecycle workflow status of this revision.
    /// Multi-draft allowed; strictly one Active revision per scenario scope.
    /// </summary>
    public RevisionStatusEnum Status { get; set; } = RevisionStatusEnum.Draft;

    /// <summary>
    /// Immutable email or identifier of the user or batch process that created this revision.
    /// Used to automatically re-assign the revision upon review approval or rejection.
    /// </summary>
    public required string CreatorEmail { get; set; }

    /// <summary>
    /// Semicolon-delimited list of currently assigned users/owners for this revision.
    /// When Draft/Approved: assigned to Creator. When ReadyForReview: assigned to Reviewers.
    /// </summary>
    public string? AssignedToEmails { get; set; }

    /// <summary>
    /// Semicolon-delimited list of reviewer emails designated when submitted for review.
    /// </summary>
    public string? ReviewerEmails { get; set; }

    /// <summary>
    /// Notes or comments recorded during review sign-off or rejection (e.g. "Rejected: Missing Q4 milestones").
    /// </summary>
    public string? ReviewComments { get; set; }

    /// <summary>
    /// Timestamp when this revision was submitted for review.
    /// </summary>
    public DateTime? SubmittedForReviewAtUtc { get; set; }

    /// <summary>
    /// Timestamp when this revision was approved by reviewers.
    /// </summary>
    public DateTime? ApprovedAtUtc { get; set; }

    /// <summary>
    /// User who signed off and approved this revision.
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Timestamp when this revision was promoted to the single Active baseline.
    /// </summary>
    public DateTime? ActivatedAtUtc { get; set; }

    /// <summary>
    /// User who activated this revision.
    /// </summary>
    public string? ActivatedBy { get; set; }

    /// <summary>
    /// Timestamp when this revision was superseded by a newer activated revision.
    /// </summary>
    public DateTime? SupersededAtUtc { get; set; }

    /// <summary>
    /// Timestamp when this revision was cancelled (either discarded manually or auto-cancelled on activation cascade).
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>
    /// Reason explaining why this revision was cancelled.
    /// </summary>
    public string? CancellationReason { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
