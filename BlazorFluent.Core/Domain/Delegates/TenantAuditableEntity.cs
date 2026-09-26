namespace BlazorFluent.Core.Domain.Delegates;

/// <summary>
/// Combines tenant isolation, auditing, and a stable GUID identity.
/// All tenant-scoped business entities should inherit from this class.
/// </summary>
public abstract class TenantAuditableEntity<TId> : AuditableEntity<TId>, ITenantEntity
{
    public string TenantId { get; set; } = string.Empty;
}

/// <summary>
/// Guid-keyed convenience base. Use this for most tenant entities.
/// </summary>
public abstract class TenantAuditableEntity : TenantAuditableEntity<Guid>
{
    protected TenantAuditableEntity()
    {
        Id = Guid.NewGuid();
    }
}
