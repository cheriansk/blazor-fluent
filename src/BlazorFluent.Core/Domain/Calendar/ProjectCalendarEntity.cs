using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Domain.Calendar;

/// <summary>
/// Dedicated project-level calendar entry.
/// Mapped to physical table 'project_calendars'.
/// Tracks sprint ceremonies, project milestones, team workshops, and release dates.
/// </summary>
public class ProjectCalendarEntity : BaseCalendarEntity
{
    /// <summary>
    /// ID of the project governing this calendar entry.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Optional foreign key to parent program if project is part of a program.
    /// </summary>
    public Guid? ProgramId { get; set; }

    /// <summary>
    /// Optional foreign key to parent program calendar entry for program-to-project gate rollup.
    /// </summary>
    public Guid? ParentProgramCalendarId { get; set; }

    // --- Navigation Properties ---
    public ProjectEntity? Project { get; set; }
    public ProgramEntity? Program { get; set; }
    public ProgramCalendarEntity? ParentProgramCalendar { get; set; }
}
