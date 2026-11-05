using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Domain.Versioning;

namespace BlazorEnterprise.Core.Domain.Calendar;

/// <summary>
/// Dedicated project-level calendar entry.
/// Mapped to physical table 'project_calendars'.
/// Tracks sprint ceremonies, project milestones, team workshops, and release dates.
/// Implements IVersionedEntity to participate in multi-draft scenario baselines.
/// Implements IProjectScopedEntity to enforce project RBAC write rules via ProjectSecurityInterceptor.
/// </summary>
public class ProjectCalendarEntity : BaseCalendarEntity, IVersionedEntity, IProjectScopedEntity
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
    /// Stores the RelId of the parent program calendar row to ensure stable lineage across versions.
    /// </summary>
    public Guid? ParentProgramCalendarId { get; set; }

    // --- IVersionedEntity Implementation ---
    /// <summary>
    /// Foreign key identifier to the governing RevisionHeaderEntity.
    /// </summary>
    public Guid RevisionHeaderId { get; set; }

    /// <summary>
    /// Lineage identity tracking this logical calendar row across all revisions and drafts.
    /// Strictly immutable once set. Brand-new rows receive RelId = Id.
    /// </summary>
    public Guid RelId { get; set; }

    /// <summary>
    /// Formal schedule baseline revision version number matching the RevisionHeaderEntity.
    /// </summary>
    public int VerNum { get; set; } = 1;

    /// <summary>
    /// Soft relation: The primary key Id of the parent version row from which this row was cloned.
    /// Guid.Empty indicates a root baseline or brand-new row.
    /// </summary>
    public Guid ParentRowId { get; set; } = Guid.Empty;

    public void SetVersionNumber(int newVersion)
    {
        if (newVersion <= VerNum)
        {
            throw new InvalidOperationException($"New calendar version ({newVersion}) must be greater than current version ({VerNum}).");
        }
        VerNum = newVersion;
    }

    // --- Navigation Properties ---
    public RevisionHeaderEntity? RevisionHeader { get; set; }
    public ProjectEntity? Project { get; set; }
    public ProgramEntity? Program { get; set; }
    public ProgramCalendarEntity? ParentProgramCalendar { get; set; }

    /// <summary>
    /// Governing parent timeline row when this entry is a Milestone.
    /// </summary>
    public ProjectCalendarEntity? ParentTimeline { get; set; }

    /// <summary>
    /// Child milestones scheduled under this timeline row.
    /// </summary>
    public ICollection<ProjectCalendarEntity> Milestones { get; set; } = new List<ProjectCalendarEntity>();
}
