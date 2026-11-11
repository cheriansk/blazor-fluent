using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// ⚠️ ARCHITECTURAL WARNING: CRITICAL FRAMEWORK MARKER INTERFACES
/// DO NOT MODIFY, ALTER, OR REMOVE any interfaces in this file without architectural review.
/// These interfaces drive EF Core SaveChanges interceptors (AuditableEntityInterceptor, ProjectSecurityInterceptor)
/// and Named Query Filters (QueryFilters.Tenant, QueryFilters.SoftDelete) across the modular monolith.
/// </summary>

/// <summary>
/// Exempts an entity from Level 2 before/after property diff logging in audit.AuditRecords.
/// Applied to AuditRecordEntity and JobExecutionEntity to prevent infinite recursive audit loops.
/// </summary>
public interface IAuditExemptEntity { }

/// <summary>
/// Explicitly opts an entity OUT of tenant-level query filtering (e.g. TenantEntity, UserEntity, KnowledgeArticleReviewerEntity, ImpersonationGrantEntity).
/// AppDbContext enforces at startup that every entity either inherits TenantScopedEntity OR implements IGlobalEntity (fail-closed guard).
/// </summary>
public interface IGlobalEntity { }

/// <summary>
/// Enables automatic soft deletion. When DbContext.Remove() is invoked on this entity,
/// AuditableEntityInterceptor intercepts the operation, setting IsDeleted = true instead of issuing a SQL DELETE.
/// Filtered out by default via EF Core query filter: QueryFilters.SoftDelete.
/// </summary>
public interface ISoftDeletableEntity
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>
/// Marks an entity as hard-deletable. When DbContext.Remove() is invoked,
/// a physical SQL DELETE is generated and executed in the database.
/// Mutually exclusive with <see cref="ISoftDeletableEntity"/> and <see cref="IImmutableEntity"/>.
/// </summary>
public interface IHardDeletableEntity
{
}

/// <summary>
/// Marks an entity as strictly immutable and append-only (WORM: Write-Once, Read-Many).
/// Calling DbContext.Remove() or mutating this entity triggers an immediate exception.
/// Mutually exclusive with <see cref="ISoftDeletableEntity"/> and <see cref="IHardDeletableEntity"/>.
/// </summary>
public interface IImmutableEntity
{
}

/// <summary>
/// Enforces effective dating (temporal validity) on database records.
/// Standardizes start and optional end date tracking across business entities (contracts, tenants, rate cards).
/// Guarded by AuditableEntityInterceptor ensuring EndDate is never before StartDate.
/// Queryable via LINQ extension query.WhereActive().
/// </summary>
public interface IEffectiveDatedEntity
{
    DateTime StartDate { get; set; }
    DateTime? EndDate { get; set; }
}

/// <summary>
/// Enforces revision and version tracking on database records.
/// Version numbers are managed via RevisionHeaderEntity.
/// RelId identifies the logical business entity across versions/drafts.
/// ParentRowId provides soft lineage back to the source version row (Guid.Empty for root/new records).
/// AuditableEntityInterceptor validates that RelId and VerNum are strictly immutable after creation.
/// </summary>
public interface IVersionedEntity
{
    Guid RevisionHeaderId { get; set; }
    Guid RelId { get; set; }
    int VerNum { get; set; }
    Guid ParentRowId { get; set; }

    /// <summary>
    /// Explicitly updates the version number while enforcing table-specific validation rules.
    /// </summary>
    /// <param name="newVersion">The new version number (must be greater than current VerNum).</param>
    void SetVersionNumber(int newVersion);
}

/// <summary>
/// Explicitly marks an entity as not subject to multi-draft scenario versioning.
/// Mutually exclusive with <see cref="IVersionedEntity"/>.
/// Every concrete entity in the system must implement either IVersionedEntity OR IUnversionedEntity.
/// </summary>
public interface IUnversionedEntity
{
}
