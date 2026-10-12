using BlazorFluent.Core.DataListTypes;
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
    public Guid TargetItemId { get; set; }

    /// <summary>
    /// Classification type of the target work item.
    /// </summary>
    public TaskItemType TargetItemType { get; set; } = TaskItemType.Task;

    /// <summary>
    /// User identifier of the author.
    /// </summary>
    public string AuthorUserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the author at the time of posting.
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Email of the author for display and notifications.
    /// </summary>
    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>
    /// Body text of the discussion comment (Markdown or plain text).
    /// </summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// Exact UTC timestamp when the comment was posted.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
