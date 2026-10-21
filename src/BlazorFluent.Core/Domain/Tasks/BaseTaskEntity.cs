using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Abstract base class for all work items across the enterprise.
/// Tied strictly to tenant via sequential UUIDv7 <see cref="TenantAuditableEntity.TenantId"/>.
/// Implements <see cref="IScheduledWorkEntity"/> for baseline vs. actual schedule tracking.
/// Implements <see cref="ISoftDeletableEntity"/> for forensic auditability.
/// </summary>
public abstract class BaseTaskEntity : TenantAuditableEntity, IScheduledWorkEntity, ISoftDeletableEntity, ISourceTrackedEntity, IEncryptedPayloadEntity
{
    /// <summary>
    /// Organizational level at which this work item exists (Tenant, Program, Project, User).
    /// </summary>
    public required TaskLevelEnum Level { get; set; }

    /// <summary>
    /// Concise headline title of the work item. Required.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Detailed description, reproduction steps, or context.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Urgency and priority rating (Low, Medium, High, Urgent).
    /// </summary>
    public required TaskPriorityEnum Priority { get; set; } 

    /// <summary>
    /// Lifecycle status of the work item.
    /// </summary>
    public required TaskStatusEnum Status { get; set; } 

    /// <summary>
    /// Earliest planned kickoff date for this work item.
    /// Part of <see cref="IScheduledWorkEntity"/>.
    /// </summary>
    public required DateTime StartDate { get; set; }

    /// <summary>
    /// Baseline target completion deadline (UTC). Mandatory for every work item.
    /// Drives cadence notifications and calendar deadline plotting.
    /// Part of <see cref="IScheduledWorkEntity"/>.
    /// </summary>
    public required DateTime DueDate { get; set; }

    /// <summary>
    /// Formally approved target deadline extension (UTC).
    /// Part of <see cref="IScheduledWorkEntity"/>.
    /// </summary>
    public DateTime? RevisedDueDate { get; set; }

    /// <summary>
    /// Planned baseline completion or close timestamp.
    /// Part of <see cref="IScheduledWorkEntity"/>.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Actual timestamp (UTC) when execution commenced (stamped when status becomes InProgress).
    /// Part of <see cref="IScheduledWorkEntity"/>.
    /// </summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>
    /// Actual timestamp (UTC) when work completed (stamped when status becomes Completed or Closed).
    /// Part of <see cref="IScheduledWorkEntity"/>.
    /// </summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// Computed schedule delivery variance in calendar days against revised or baseline due date.
    /// </summary>
    [NotMapped]
    public int ScheduleVarianceDays =>
        ((ActualEndDate ?? DateTime.UtcNow).Date - (RevisedDueDate ?? DueDate).Date).Days;

    /// <summary>
    /// Email of the user who originated or reported this work item.
    /// </summary>
    public string? ReporterEmail { get; set; }

    /// <summary>
    /// Semicolon-delimited list of assignee email addresses.
    /// </summary>
    public string? AssigneeEmails { get; set; }

    /// <summary>
    /// Semicolon-delimited list of stakeholder emails who monitor this work item.
    /// Watchers receive automated status change alerts and see the item on their calendars.
    /// </summary>
    public string? WatcherEmails { get; set; }

    /// <summary>
    /// Comma-delimited list of classification labels/tags.
    /// </summary>
    public string? Labels { get; set; }

    // --- Hierarchy Scoping ---
    public Guid? ProgramId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? UserId { get; set; }

    // --- ISourceTrackedEntity ---
    public Guid? SourceRowId { get; set; }

    // --- IEncryptedPayloadEntity ---
    public bool IsEncryptedAtRest { get; set; }
    public byte[]? EncryptedPayloadBytes { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Derived Unmapped Metadata ---
    [NotMapped]
    public bool IsClosed => Status is TaskStatusEnum.Closed or TaskStatusEnum.Cancelled;

    [NotMapped]
    public DateTime? ClosedAtUtc => IsClosed ? ActualEndDate ?? EndDate ?? Updated : null;

    [NotMapped]
    public string? ClosedBy => IsClosed ? UpdatedBy : null;

    /// <summary>
    /// Parses the semicolon-separated assignees into clean trimmed email addresses.
    /// </summary>
    public IReadOnlyList<string> GetParsedAssigneeEmails() =>
        string.IsNullOrWhiteSpace(AssigneeEmails)
            ? []
            : AssigneeEmails.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses the semicolon-separated watchers into clean trimmed email addresses.
    /// </summary>
    public IReadOnlyList<string> GetParsedWatcherEmails() =>
        string.IsNullOrWhiteSpace(WatcherEmails)
            ? []
            : WatcherEmails.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses the comma-separated labels into clean trimmed tag strings.
    /// </summary>
    public IReadOnlyList<string> GetParsedLabels() =>
        string.IsNullOrWhiteSpace(Labels)
            ? []
            : Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
