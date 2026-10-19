using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Project milestone entity partitioning deliverables and delivery deadlines.
/// Enforces tenant and project isolation.
/// </summary>
public class ProjectMilestoneEntity : TenantAuditableEntity, IProjectScopedEntity, IEffectiveDatedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// ID of the project to which this milestone belongs.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// Milestone headline (e.g. 'Sprint 1', 'UAT', 'Go-Live Release').
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Detailed scope summary or acceptance criteria for this milestone.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Planned kickoff date (UTC). Part of <see cref="IEffectiveDatedEntity"/>.
    /// </summary>
    public required DateTime StartDate { get; set; }

    /// <summary>
    /// Target completion deadline (UTC). Part of <see cref="IEffectiveDatedEntity"/>.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Current lifecycle status (Planned, Active, Completed, AtRisk, Cancelled).
    /// </summary>
    public required MilestoneStatus Status { get; set; }

    /// <summary>
    /// Sequential presentation order within the project roadmap.
    /// </summary>
    public int OrderIndex { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
