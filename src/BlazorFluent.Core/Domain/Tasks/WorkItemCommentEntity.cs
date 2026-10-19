using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Universal polymorphic discussion comment attached to any work item
/// (Task, SubTask, Risk, Assumption, Issue, or BlockerDependency).
/// </summary>
public class WorkItemCommentEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    /// <summary>
    /// ID of the target work item receiving this comment.
    /// </summary>
    public required Guid TargetItemId { get; set; }

    /// <summary>
    /// Classification type of the target work item.
    /// </summary>
    public required TaskItemType TargetItemType { get; set; }

    /// <summary>
    /// User identifier of the author.
    /// </summary>
    public required string AuthorUserId { get; set; }

    /// <summary>
    /// Display name of the author at the time of posting.
    /// </summary>
    public required string AuthorName { get; set; }

    /// <summary>
    /// Email of the author for display and notifications.
    /// </summary>
    public required string AuthorEmail { get; set; }

    /// <summary>
    /// Body text of the discussion comment (Markdown or plain text).
    /// </summary>
    public required string CommentText { get; set; }

    /// <summary>
    /// Exact UTC timestamp when the comment was posted.
    /// </summary>
    public required DateTime CreatedAtUtc { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
