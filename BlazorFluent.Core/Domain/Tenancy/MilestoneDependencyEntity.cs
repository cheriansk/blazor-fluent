using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Predecessor relationship between project milestones.
/// Example: 'UAT' depends on 'Build' and 'QA' completing.
/// </summary>
public class MilestoneDependencyEntity : TenantAuditableEntity, IProjectScopedEntity
{
    /// <summary>
    /// ID of the project owning both milestones.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The dependent milestone (successor) waiting for predecessor to complete.
    /// </summary>
    public Guid MilestoneId { get; set; }

    /// <summary>
    /// The prerequisite milestone (predecessor) that must be finished first.
    /// </summary>
    public Guid DependsOnMilestoneId { get; set; }

    /// <summary>
    /// Optional context or constraint notes.
    /// </summary>
    public string? Notes { get; set; }
}
