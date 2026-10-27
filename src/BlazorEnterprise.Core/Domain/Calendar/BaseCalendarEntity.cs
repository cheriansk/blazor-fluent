using System;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Calendar;

/// <summary>
/// Universal abstract base class for all calendar entities across Program, Project, and User levels.
/// Models milestone target dates, sprint cadences, release schedules, phase gates, and delivery deadlines.
/// Tied to tenant via sequential UUIDv7 <see cref="TenantAuditableEntity.TenantId"/>.
/// Implements <see cref="IEffectiveDatedEntity"/> (StartDate and EndDate) and <see cref="ISoftDeletableEntity"/>.
/// Implements <see cref="IVersionedEntity"/> to maintain immutable baseline revision slippage history.
/// </summary>
public abstract class BaseCalendarEntity : TenantAuditableEntity, IEffectiveDatedEntity, IVersionedEntity, ISoftDeletableEntity
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
    /// Formal schedule baseline revision version number (1, 2, 3...).
    /// Initialized to 1 upon creation; incremented on schedule re-baselining to preserve slippage audit trails.
    /// </summary>
    public int VerNum { get; set; } = 1;

    public void SetVersionNumber(int newVersion)
    {
        if (newVersion <= VerNum)
        {
            throw new InvalidOperationException($"New calendar version ({newVersion}) must be greater than current version ({VerNum}).");
        }
        VerNum = newVersion;
    }

    /// <summary>
    /// Classification category of this calendar entry (Milestone, Sprint, Release, Deadline, etc.).
    /// </summary>
    public required CalendarEntryTypeEnum EntryType { get; set; } 

    /// <summary>
    /// Current scheduling status (Scheduled, Confirmed, InProgress, Completed, Cancelled).
    /// </summary>
    public required CalendarEntryStatusEnum Status { get; set; }

    /// <summary>
    /// Optional foreign key to an associated work item (Task, Subtask, Risk, etc.).
    /// </summary>
    public Guid? AssociatedTaskId { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
