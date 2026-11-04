using BlazorEnterprise.Core.Domain.Tenancy;

namespace BlazorEnterprise.Core.Domain.Calendar;

/// <summary>
/// Dedicated program-level calendar entry.
/// Mapped to physical table 'program_calendars'.
/// Tracks program releases, steering committee sessions, and cross-project milestones.
/// </summary>
public class ProgramCalendarEntity : BaseCalendarEntity
{
    /// <summary>
    /// ID of the program governing this calendar entry.
    /// </summary>
    public Guid ProgramId { get; set; }

    // --- Navigation Properties ---
    public ProgramEntity? Program { get; set; }

    /// <summary>
    /// Governing parent timeline row when this entry is a Milestone.
    /// </summary>
    public ProgramCalendarEntity? ParentTimeline { get; set; }

    /// <summary>
    /// Child milestones scheduled under this timeline row.
    /// </summary>
    public ICollection<ProgramCalendarEntity> Milestones { get; set; } = new List<ProgramCalendarEntity>();
}
