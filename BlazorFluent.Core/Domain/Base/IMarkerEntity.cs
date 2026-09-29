namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// ⚠️ ARCHITECTURAL WARNING: CRITICAL FRAMEWORK MARKER INTERFACES
/// DO NOT MODIFY, ALTER, OR REMOVE any interfaces in this file.
/// These interfaces drive EF Core SaveChanges interceptors (AuditableEntityInterceptor, ProjectSecurityInterceptor)
/// and Named Query Filters (QueryFilters.Tenant, QueryFilters.SoftDelete) across the modular monolith.
/// </summary>
public interface IMarkerEntity { }

/// <summary>
/// Enforces multi-tenant data isolation. All tenant-scoped entities MUST implement this interface.
/// Protected by EF Core named query filter: QueryFilters.Tenant.
/// TenantId is immutable after insertion (enforced by AuditableEntityInterceptor).
/// </summary>
public interface ITenantEntity : IMarkerEntity
{
    string TenantId { get; set; }
}

/// <summary>
/// Explicitly opts an entity OUT of tenant-level query filtering (e.g. TenantEntity, UserEntity, JobExecutionEntity).
/// AppDbContext enforces at startup that every entity implements either ITenantEntity or IGlobalEntity (fail-closed guard).
/// </summary>
public interface IGlobalEntity : IMarkerEntity { }

/// <summary>
/// Enables automatic soft deletion. When DbContext.Remove() is invoked on this entity,
/// AuditableEntityInterceptor intercepts the operation, setting IsDeleted = true instead of issuing a SQL DELETE.
/// Filtered out by default via EF Core query filter: QueryFilters.SoftDelete.
/// </summary>
public interface ISoftDeletableEntity : IMarkerEntity
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>
/// Scopes an entity to a specific project workspace.
/// ProjectSecurityInterceptor intercepts write operations (Insert/Update/Delete) on this entity,
/// validating the user's ProjectRole against required write permissions.
/// </summary>
public interface IProjectScopedEntity : IMarkerEntity
{
    Guid ProjectId { get; set; }
}

/// <summary>
/// Enables Level 1 row-level auditing. AuditableEntityInterceptor automatically stamps
/// Created/CreatedBy on insert, and Updated/UpdatedBy on update.
/// </summary>
public interface IAuditableEntity : IMarkerEntity
{
    DateTime Created { get; set; }
    string? CreatedBy { get; set; }
    DateTime? Updated { get; set; }
    string? UpdatedBy { get; set; }
}

/// <summary>
/// Exempts an entity from Level 2 before/after property diff logging in audit.AuditRecords.
/// Applied to AuditRecordEntity and JobExecutionEntity to prevent infinite recursive audit loops.
/// </summary>
public interface IAuditExemptEntity : IMarkerEntity { }

/// <summary>
/// Enforces effective dating (temporal validity) on database records.
/// Standardizes start and optional end date tracking across business entities (contracts, tenants, rate cards).
/// Guarded by AuditableEntityInterceptor ensuring EndDate is never before StartDate.
/// Queryable via LINQ extension query.WhereActive().
/// </summary>
public interface IEffectiveDatedEntity : IMarkerEntity
{
    DateTime StartDate { get; set; }
    DateTime? EndDate { get; set; }
}

/// <summary>
/// Enforces revision and version tracking on database records.
/// Version numbers are NOT blindly auto-incremented on every update; instead, the entity encapsulates
/// its own SetVersionNumber(newVersion) method to enforce table-specific validation rules.
/// AuditableEntityInterceptor validates that VerNum is initialized >= 1 upon insert.
/// </summary>
public interface IVersionedEntity : IMarkerEntity
{
    int VerNum { get; set; }

    /// <summary>
    /// Explicitly updates the version number while enforcing table-specific validation rules.
    /// </summary>
    /// <param name="newVersion">The new version number (must be greater than current VerNum).</param>
    void SetVersionNumber(int newVersion);
}
