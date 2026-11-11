using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Calendar;

/// <summary>
/// Unified versioned timeline and milestone calendar entity across Program and Project levels.
/// Associated with Program or Project via Hierarchy Scoping (<c>ProgramRelationEntity</c> / <c>ProjectRelationEntity</c>).
/// Implements <see cref="IVersionedEntity"/> to participate in multi-draft scenario baselines.
/// </summary>
public class CalendarEntity : BaseCalendarEntity, IVersionedEntity
{
    
    /// <summary>
    /// Optional identifier linking project-level milestone gates to parent program timeline entries.
    /// Stores the RelId of the parent program calendar row for cross-revision lineage.
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
    public int VerNum { get; set; }

    /// <summary>
    /// Soft relation: The primary key Id of the parent version row from which this row was cloned.
    /// Guid.Empty indicates a root baseline or brand-new row.
    /// </summary>
    public Guid ParentRowId { get; set; }

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

    /// <summary>
    /// Governing parent timeline row when this entry is a Milestone.
    /// </summary>
    public CalendarEntity? ParentTimeline { get; set; }

    /// <summary>
    /// Child milestones scheduled under this timeline row.
    /// </summary>
    public ICollection<CalendarEntity> Milestones { get; set; } = new List<CalendarEntity>();

    // --- [NotMapped] Convenience Properties populated via hierarchy scoping ---
    // Inherits ProgramId from ProjectScopedEntity

    // Inherits ProjectId from ProjectScopedEntity
}
