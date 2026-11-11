using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// Architectural base class for all tenant-partitioned business entities.
/// Combines sequential UUIDv7 primary key, Level 1 audit properties, and mandatory TenantId isolation.
/// Protected by EF Core named query filter: QueryFilters.Tenant.
/// </summary>
public abstract class TenantScopedEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public virtual TaskLevelEnum Level { get; set; }
}
