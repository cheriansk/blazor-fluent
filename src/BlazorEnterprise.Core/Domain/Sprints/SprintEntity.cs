using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Sprints;

/// <summary>
/// Project-scoped sprint iteration. Sprints maintain an audit trail but are container records (not versioned).
/// </summary>
public class SprintEntity : ProjectScopedEntity, ISoftDeletableEntity, IUnversionedEntity
{
    // Inherits ProjectId from ProjectScopedEntity

    public int SprintNumber { get; set; }

    public required string SprintName { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? Comments { get; set; }

    public SprintStatusEnum Status { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Prouerties ---
    public ProjectEntity? Project { get; set; }
    public ICollection<SprintItemVersionEntity> Items { get; set; } = new List<SprintItemVersionEntity>();
    public ICollection<SprintAuditLogEntity> AuditLogs { get; set; } = new List<SprintAuditLogEntity>();
}
