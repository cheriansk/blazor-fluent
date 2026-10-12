using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Domain.Calendar;

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

    // --- Navigation Property ---
    public ProgramEntity? Program { get; set; }
}
