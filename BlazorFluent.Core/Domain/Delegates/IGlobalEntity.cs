namespace BlazorFluent.Core.Domain.Delegates;

/// <summary>
/// Marker interface indicating that an entity is global (host-wide) and deliberately
/// bypasses automatic multi-tenant row-level filtering.
/// 
/// All entities in the application must either implement <see cref="ITenantEntity"/> (tenant-scoped)
/// or <see cref="IGlobalEntity"/> (global/host-only). Unscoped entities are rejected by design
/// to prevent accidental data leaks.
/// </summary>
public interface IGlobalEntity
{
}
