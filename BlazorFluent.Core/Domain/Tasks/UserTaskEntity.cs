using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Represents a work item or task strictly partitioned per-tenant and per-project.
/// Supports multi-assignee tracking via semicolon-delimited emails and Jira-style tag labels.
/// </summary>
public class UserTaskEntity : TenantAuditableEntity, IProjectScopedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// ID of the project to which this task belongs.
    /// Guarded by ProjectSecurityInterceptor and QueryFilters.Tenant.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Concise headline title of the task. Required.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed Markdown or plain-text description and reproduction steps.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Urgency and priority rating (Low, Medium, High, Urgent).
    /// </summary>
    public UserTaskPriority Priority { get; set; } = UserTaskPriority.Medium;

    /// <summary>
    /// Current lifecycle status (Open, InProgress, Closed).
    /// </summary>
    public UserTaskStatus Status { get; set; } = UserTaskStatus.Open;

    /// <summary>
    /// Target completion deadline. Drives swimlane column placement and daily Teams digest.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Semicolon-delimited list of assignee email addresses (e.g. 'dev1@company.com; dev2@company.com').
    /// Each assignee receives an automated in-app notification upon task creation.
    /// </summary>
    public string AssigneeEmails { get; set; } = string.Empty;

    /// <summary>
    /// Comma-delimited list of Jira-style classification labels/tags (e.g. 'Frontend, Bug, HighPriority').
    /// Rendered as interactive FluentBadge pills on task cards.
    /// </summary>
    public string Labels { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating whether the task has been marked as completed/resolved.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Timestamp when the task was moved to the Closed status.
    /// </summary>
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>
    /// User identifier of the person who closed the task.
    /// </summary>
    public string? ClosedBy { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public ProjectEntity? Project { get; set; }
    public ICollection<UserTaskCommentEntity> Comments { get; set; } = new List<UserTaskCommentEntity>();

    /// <summary>
    /// Parses the semicolon-separated assignees into clean trimmed email addresses.
    /// </summary>
    public IReadOnlyList<string> GetParsedAssigneeEmails() =>
        string.IsNullOrWhiteSpace(AssigneeEmails)
            ? Array.Empty<string>()
            : AssigneeEmails.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses the comma-separated labels into clean trimmed tag strings.
    /// </summary>
    public IReadOnlyList<string> GetParsedLabels() =>
        string.IsNullOrWhiteSpace(Labels)
            ? Array.Empty<string>()
            : Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
