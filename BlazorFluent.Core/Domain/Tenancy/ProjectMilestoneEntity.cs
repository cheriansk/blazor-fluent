using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Project milestone entity partitioning deliverables and delivery deadlines.
/// Enforces tenant and project isolation.
/// </summary>
public class ProjectMilestoneEntity : TenantAuditableEntity, IProjectScopedEntity
{
    /// <summary>
    /// ID of the project to which this milestone belongs.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Milestone headline (e.g. 'Sprint 1', 'UAT', 'Go-Live Release').
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed scope summary or acceptance criteria for this milestone.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Planned kickoff date (UTC).
    /// </summary>
    public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Target completion deadline (UTC).
    /// </summary>
    public DateTime EndDateUtc { get; set; } = DateTime.UtcNow.AddDays(14);

    /// <summary>
    /// Current lifecycle status (Planned, Active, Completed, AtRisk, Cancelled).
    /// </summary>
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Planned;

    /// <summary>
    /// Sequential presentation order within the project roadmap.
    /// </summary>
    public int OrderIndex { get; set; }
}
