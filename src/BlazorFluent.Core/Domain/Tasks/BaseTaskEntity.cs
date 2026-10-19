using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Abstract base class for all work items across the enterprise.
/// Tied strictly to tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="IEffectiveDatedEntity"/> for temporal contract validity.
/// Implements <see cref="ISoftDeletableEntity"/> for forensic auditability.
/// </summary>
public abstract class BaseTaskEntity : TenantAuditableEntity, IEffectiveDatedEntity, ISoftDeletableEntity, ISourceTrackedEntity, IEncryptedPayloadEntity
{
    /// <summary>
    /// Organizational level at which this work item exists (Tenant, Program, Project, User).
    /// </summary>
    public TaskLevel Level { get; set; }

    /// <summary>
    /// Concise headline title of the work item. Required.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description, reproduction steps, or context.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Urgency and priority rating (Low, Medium, High, Urgent).
    /// </summary>
    public TaskPriority Priority { get; set; } 

    /// <summary>
    /// Lifecycle status of the work item.
    /// </summary>
    public TaskStatus Status { get; set; } 

    /// <summary>
    /// Earliest planned kickoff date for this work item.
    /// Part of <see cref="IEffectiveDatedEntity"/>.
    /// </summary>
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Target completion deadline (UTC). Mandatory for every work item.
    /// Drives cadence notifications and calendar deadline plotting.
    /// </summary>
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);

    /// <summary>
    /// Actual completion or close timestamp.
    /// Part of <see cref="IEffectiveDatedEntity"/>. Populated automatically when Status reaches Closed or Cancelled.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Email of the user who originated or reported this work item.
    /// </summary>
    public string ReporterEmail { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon-delimited list of assignee email addresses.
    /// </summary>
    public string AssigneeEmails { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon-delimited list of stakeholder emails who monitor this work item.
    /// Watchers receive automated status change alerts and see the item on their calendars.
    /// </summary>
    public string WatcherEmails { get; set; } = string.Empty;

    /// <summary>
    /// Comma-delimited list of classification labels/tags.
    /// </summary>
    public string Labels { get; set; } = string.Empty;

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
    public bool IsClosed => Status is TaskStatus.Closed or TaskStatus.Cancelled;

    [NotMapped]
    public DateTime? ClosedAtUtc => IsClosed ? EndDate ?? Updated : null;

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
