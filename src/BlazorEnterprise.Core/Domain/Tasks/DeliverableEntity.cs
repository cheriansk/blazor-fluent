using System;
using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Enterprise deliverable entity modeling tangible outputs, review artifacts, sign-offs, and meeting outcomes.
/// Extends <see cref="BaseTaskEntity"/> to inherit enterprise scheduling, levels, assignments, watchers, and variance tracking.
/// Linked to parent tasks, RAID items, or calendar milestones/meeting outcomes via <see cref="BlazorEnterprise.Core.Domain.Relations.DeliverableRelationEntity"/>.
/// Subtasks and WorkDependencies are strictly excluded from having deliverables.
/// Mapped via EF Core to physical table 'tasks.Deliverables'.
/// </summary>
public class DeliverableEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    /// <summary>
    /// Classification of the parent work item holding this deliverable (Task, Risk, Issue, Assumption, CalendarGate, Workflow).
    /// Subtasks and WorkDependencies are strictly excluded.
    /// </summary>
    public required DeliverableParentTypeEnum ParentType { get; set; }

    /// <summary>
    /// Primary key identifier of the parent work item holding this deliverable.
    /// </summary>
    public required Guid ParentItemId { get; set; }

    /// <summary>
    /// Lifecycle sign-off and approval state of this deliverable (Draft, UnderReview, Approved, Rejected).
    /// </summary>
    public required DeliverableStatusEnum SignOffStatus { get; set; }

    /// <summary>
    /// Optional URI or file storage reference pointing to the completed artifact or document.
    /// </summary>
    public string? ArtifactUrl { get; set; }

    /// <summary>
    /// Reviewer feedback, acceptance criteria evaluation, or rejection remarks.
    /// </summary>
    public string? SignOffNotes { get; set; }

    /// <summary>
    /// Email or identifier of the lead or authority who formally approved or rejected the deliverable.
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Timestamp in UTC when formal sign-off occurred.
    /// </summary>
    public DateTime? ApprovedAtUtc { get; set; }

    // --- IMilestoneLinkedEntity Implementation ---
    [NotMapped]
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Formally approves this deliverable, stamping the approver and timestamp.
    /// </summary>
    public void Approve(string approverEmail, DateTime utcNow, string? notes = null)
    {
        SignOffStatus = DeliverableStatusEnum.Approved;
        ApprovedBy = approverEmail;
        ApprovedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            SignOffNotes = notes;
        }
    }

    /// <summary>
    /// Rejects this deliverable back to draft for revisions.
    /// </summary>
    public void Reject(string approverEmail, DateTime utcNow, string reason)
    {
        SignOffStatus = DeliverableStatusEnum.Rejected;
        ApprovedBy = approverEmail;
        ApprovedAtUtc = utcNow;
        SignOffNotes = reason;
    }
}
