using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Calendar;

/// <summary>
/// Universal abstract base class for all calendar entities across Program, Project, and User levels.
/// Models milestone target dates, sprint cadences, release schedules, phase gates, and delivery deadlines.
/// Tied to tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="IEffectiveDatedEntity"/> (StartDate and EndDate) and <see cref="ISoftDeletableEntity"/>.
/// </summary>
public abstract class BaseCalendarEntity : TenantAuditableEntity, IEffectiveDatedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// Title for the Event
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Event description, scope summary, or release notes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Effective start date in UTC (Date-level precision).
    /// </summary>
    public required DateTime StartDate { get; set; }

    /// <summary>
    /// Effective end date in UTC (Date-level precision).
    /// Required for all entries. For single-day milestones, EndDate equals StartDate.
    /// </summary>
    public DateTime? EndDate { get; set; } 

    /// <summary>
    /// Classification category of this calendar entry (Milestone, Sprint, Release, Deadline, etc.).
    /// </summary>
    public required CalendarEntryType EntryType { get; set; } 

    /// <summary>
    /// Current scheduling status (Scheduled, Confirmed, InProgress, Completed, Cancelled).
    /// </summary>
    public required CalendarEntryStatus Status { get; set; }

    /// <summary>
    /// Optional foreign key to an associated work item (Task, Subtask, Risk, etc.).
    /// </summary>
    public Guid? AssociatedTaskId { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
