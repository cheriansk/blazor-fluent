using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Project milestone entity partitioning deliverables and delivery deadlines.
/// Enforces tenant and project isolation.
/// </summary>
public class ProjectMilestoneEntity : TenantAuditableEntity, IProjectScopedEntity, ISoftDeletableEntity
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
    /// Planned kickoff date (UTC).
    /// </summary>
    public required DateTime StartDateUtc { get; set; }

    /// <summary>
    /// Target completion deadline (UTC).
    /// </summary>
    public required DateTime EndDateUtc { get; set; }

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
