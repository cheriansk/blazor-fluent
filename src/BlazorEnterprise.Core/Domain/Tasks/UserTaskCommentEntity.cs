using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Chronological Jira-style discussion comment attached to a parent task.
/// Maintained within tenant and project scope with audit dates and soft-deletion support.
/// Enforces 3NF relational normalization by persisting only <see cref="AuthorUserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="AuthorUser"/>.
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
    /// Foreign key to the author <see cref="UserEntity"/>.
    /// </summary>
    public required Guid AuthorUserEntityId { get; set; }

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

    // --- Navigation Properties ---
    public TaskEntity? Task { get; set; }
    public UserEntity? AuthorUser { get; set; }

    // --- [NotMapped] Navigation Forwarders ---
    [NotMapped]
    public string AuthorUserId => AuthorUserEntityId.ToString();

    [NotMapped]
    public string AuthorEmail => AuthorUser?.Email ?? string.Empty;

    [NotMapped]
    public string AuthorName => !string.IsNullOrWhiteSpace(AuthorUser?.FullName) ? AuthorUser.FullName : (AuthorUser?.Email ?? "System");
}
