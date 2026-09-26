using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Delegates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorFluent.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITenantContext _tenantContext;

    public const string CreatedProperty = "Created";
    public const string CreatedByProperty = "CreatedBy";
    public const string UpdatedProperty = "Updated";
    public const string UpdatedByProperty = "UpdatedBy";
    public const string TenantIdProperty = "TenantId";

    public AuditableEntityInterceptor(
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ITenantContext tenantContext)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context == null) return;

        var now = _dateTimeProvider.Now;
        var currentUserId = _currentUser.UserId ?? "system";

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
            {
                continue;
            }

            // ─── TENANT SECURITY ──────────────────────────────────────────────────────
            if (entry.Entity is ITenantEntity tenantEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    // If TenantId was not set by application code, inject the current context's tenant.
                    // Host users with IsHost=true can write to any tenant explicitly.
                    if (string.IsNullOrWhiteSpace(tenantEntity.TenantId))
                    {
                        if (string.IsNullOrWhiteSpace(_tenantContext.TenantId))
                        {
                            // Fail-closed: never persist an entity without a TenantId
                            throw new InvalidOperationException(
                                $"Cannot save entity '{entry.Entity.GetType().Name}': " +
                                $"TenantId is not set and no active tenant is available in ITenantContext. " +
                                $"Ensure the user session is fully initialized before writing tenant-scoped data.");
                        }

                        tenantEntity.TenantId = _tenantContext.TenantId;
                    }
                    else if (!_tenantContext.IsHost &&
                             tenantEntity.TenantId != _tenantContext.TenantId)
                    {
                        // Security: a non-host user is trying to write to a different tenant's data.
                        throw new InvalidOperationException(
                            $"Security violation: attempt to insert entity '{entry.Entity.GetType().Name}' " +
                            $"into tenant '{tenantEntity.TenantId}' while active tenant is '{_tenantContext.TenantId}'.");
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    // TenantId is immutable — it can never be changed after creation.
                    entry.Property(TenantIdProperty).IsModified = false;

                    // Cross-tenant write guard for non-host users
                    if (!_tenantContext.IsHost &&
                        tenantEntity.TenantId != _tenantContext.TenantId)
                    {
                        throw new InvalidOperationException(
                            $"Security violation: attempt to modify entity '{entry.Entity.GetType().Name}' " +
                            $"belonging to tenant '{tenantEntity.TenantId}' while active tenant is '{_tenantContext.TenantId}'.");
                    }
                }
            }
            // ─────────────────────────────────────────────────────────────────────────

            // ─── AUDIT FIELDS ─────────────────────────────────────────────────────────
            // 1. Strongly typed IAuditableEntity
            if (entry.Entity is IAuditableEntity auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    auditableEntity.Created = now;
                    auditableEntity.CreatedBy = currentUserId;
                    auditableEntity.Updated = null;
                    auditableEntity.UpdatedBy = null;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditableEntity.Updated = now;
                    auditableEntity.UpdatedBy = currentUserId;

                    // Ensure creation audit properties cannot be overwritten
                    entry.Property(nameof(IAuditableEntity.Created)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                }
            }
            else
            {
                // 2. Entities with shadow audit properties
                var hasCreated = entry.Metadata.FindProperty(CreatedProperty) != null;
                var hasCreatedBy = entry.Metadata.FindProperty(CreatedByProperty) != null;
                var hasUpdated = entry.Metadata.FindProperty(UpdatedProperty) != null;
                var hasUpdatedBy = entry.Metadata.FindProperty(UpdatedByProperty) != null;

                if (entry.State == EntityState.Added)
                {
                    if (hasCreated) entry.Property(CreatedProperty).CurrentValue = now;
                    if (hasCreatedBy) entry.Property(CreatedByProperty).CurrentValue = currentUserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (hasUpdated) entry.Property(UpdatedProperty).CurrentValue = now;
                    if (hasUpdatedBy) entry.Property(UpdatedByProperty).CurrentValue = currentUserId;

                    if (hasCreated) entry.Property(CreatedProperty).IsModified = false;
                    if (hasCreatedBy) entry.Property(CreatedByProperty).IsModified = false;
                }
            }
            // ─────────────────────────────────────────────────────────────────────────
        }
    }
}
