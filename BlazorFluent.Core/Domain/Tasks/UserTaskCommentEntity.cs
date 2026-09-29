using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Chronological Jira-style discussion comment attached to a parent task.
/// Maintained within tenant scope with audit dates and soft-deletion support.
/// </summary>
public class UserTaskCommentEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    /// <summary>
    /// Foreign key to the parent <see cref="UserTaskEntity"/>.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// User identifier of the comment author.
    /// </summary>
    public string AuthorUserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the author at the time of posting.
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Email of the author for display and avatar resolution.
    /// </summary>
    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>
    /// Body text of the comment (Markdown or plain text).
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

    // --- Navigation Property ---
    public UserTaskEntity? Task { get; set; }
}
