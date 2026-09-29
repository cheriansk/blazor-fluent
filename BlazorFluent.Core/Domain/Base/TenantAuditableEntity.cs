namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// ⚠️ ARCHITECTURAL WARNING: CRITICAL FRAMEWORK BASE CLASS
/// DO NOT MODIFY without architectural review.
/// Combines sequential UUIDv7 primary key, Level 1 audit properties, and mandatory TenantId isolation.
/// All tenant-scoped business entities should inherit from this class.
/// Protected by EF Core named query filter: QueryFilters.Tenant.
/// </summary>
public abstract class TenantAuditableEntity : AuditableEntity, ITenantEntity
{
    public string TenantId { get; set; } = string.Empty;
}
