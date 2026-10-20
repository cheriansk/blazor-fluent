using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Predecessor scheduling relationship (Gantt / Critical Path) between project milestones.
/// Example: 'UAT' depends on 'Build' and 'QA' completing.
/// <para>
/// <b>Note:</b> This is a pure scheduling sequence edge, NOT a RAID log item.
/// The "D" in RAID is represented by <see cref="BlazorFluent.Core.Domain.Tasks.BlockerDependencyEntity"/> (an accountable work item subtype of BaseTaskEntity).
/// </para>
/// </summary>
public class MilestoneDependencyEntity : TenantAuditableEntity, IProjectScopedEntity, IHardDeletableEntity
{
    /// <summary>
    /// ID of the project owning both milestones.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// The dependent milestone (successor) waiting for predecessor to complete.
    /// </summary>
    public required Guid MilestoneId { get; set; }

    /// <summary>
    /// The prerequisite milestone (predecessor) that must be finished first.
    /// </summary>
    public required Guid DependsOnMilestoneId { get; set; }

    /// <summary>
    /// Optional context or constraint notes.
    /// </summary>
    public string? Notes { get; set; }
}
