using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Domain.Versioning;

namespace BlazorEnterprise.Core.Domain.Calendar;

/// <summary>
/// Dedicated program-level calendar entry.
/// Mapped to physical table 'program_calendars'.
/// Tracks program releases, steering committee sessions, and cross-project milestones.
/// Implements IVersionedEntity to participate in multi-draft scenario baselines.
/// </summary>
public class ProgramCalendarEntity : BaseCalendarEntity, IVersionedEntity
{
    /// <summary>
    /// ID of the program governing this calendar entry.
    /// </summary>
    public Guid ProgramId { get; set; }

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
