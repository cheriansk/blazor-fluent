using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Chronological Jira-style discussion comment attached to a parent task.
/// Maintained within tenant and project scope with audit dates and soft-deletion support.
/// </summary>
public class UserTaskCommentEntity : TenantAuditableEntity, IProjectScopedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// Foreign key to the parent <see cref="TaskEntity"/>.
    /// </summary>
    public required Guid TaskId { get; set; }

    /// <summary>
    /// ID of the project to which this task comment belongs.
    /// Guarded by ProjectSecurityInterceptor and compound query scoping.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// User identifier of the comment author.
    /// </summary>
    public required string AuthorUserId { get; set; }

    /// <summary>
    /// Display name of the author at the time of posting.
    /// </summary>
    public required string AuthorName { get; set; }

    /// <summary>
    /// Email of the author for display and avatar resolution.
    /// </summary>
    public required string AuthorEmail { get; set; }

    /// <summary>
    /// Body text of the comment (Markdown or plain text).
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

    // --- Navigation Property ---
    public TaskEntity? Task { get; set; }
}
