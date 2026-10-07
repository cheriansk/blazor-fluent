using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Represents a work item or task strictly partitioned per-tenant and per-project.
/// Supports multi-assignee tracking, reporter identity, mandatory due date,
/// optional milestone linking (ad-hoc tasks supported), and derived unmapped completion metadata.
/// </summary>
public class UserTaskEntity : TenantAuditableEntity, IProjectScopedEntity
{
    /// <summary>
    /// ID of the project to which this task belongs.
    /// Guarded by ProjectSecurityInterceptor and QueryFilters.Tenant.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Optional foreign key to a project milestone. Null indicates an ad-hoc task.
    /// </summary>
    public Guid? MilestoneId { get; set; }

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
    /// Current lifecycle status (Open, InProgress, Closed, Cancelled).
    /// </summary>
    public UserTaskStatus Status { get; set; } = UserTaskStatus.Open;

    /// <summary>
    /// Target completion deadline (UTC). Mandatory for every task.
    /// Drives cadence notifications (T-5, T-3, T-1, T-0, and daily overdue tracking).
    /// </summary>
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);

    /// <summary>
    /// Email of the user who originated / created this task.
    /// Automatically stamped from CurrentUser upon creation.
    /// </summary>
    public string ReporterEmail { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon-delimited list of assignee email addresses (e.g. 'dev1@company.com; dev2@company.com').
    /// Each assignee receives automated in-app notifications and cadence alerts.
    /// </summary>
    public string AssigneeEmails { get; set; } = string.Empty;

    /// <summary>
    /// Comma-delimited list of Jira-style classification labels/tags (e.g. 'Frontend, Bug, HighPriority').
    /// Rendered as interactive FluentBadge pills on task cards.
    /// </summary>
    public string Labels { get; set; } = string.Empty;

    /// <summary>
    /// Derived unmapped flag indicating whether the task is completed or cancelled.
    /// Derived dynamically from Status (never stored as a redundant database column).
    /// </summary>
    [NotMapped]
    public bool IsClosed => Status is UserTaskStatus.Closed or UserTaskStatus.Cancelled;

    /// <summary>
    /// Derived unmapped timestamp when the task was closed or cancelled.
    /// Derived from the automatic audit stamp 'Updated' when IsClosed is true.
    /// </summary>
    [NotMapped]
    public DateTime? ClosedAtUtc => IsClosed ? Updated : null;

    /// <summary>
    /// Derived unmapped identifier of the user who closed or cancelled the task.
    /// Derived from the automatic audit stamp 'UpdatedBy' when IsClosed is true.
    /// </summary>
    [NotMapped]
    public string? ClosedBy => IsClosed ? UpdatedBy : null;

    /// <summary>
    /// Navigation to Jira-style discussion comments thread.
    /// </summary>
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
