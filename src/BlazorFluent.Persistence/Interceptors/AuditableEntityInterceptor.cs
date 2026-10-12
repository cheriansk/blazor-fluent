using System.Text.Json;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Utilities;
using BlazorFluent.Persistence.Context;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<AuditableEntityInterceptor> _logger;

    public const string CreatedProperty = "Created";
    public const string CreatedByProperty = "CreatedBy";
    public const string UpdatedProperty = "Updated";
    public const string UpdatedByProperty = "UpdatedBy";
    public const string TenantIdProperty = "TenantId";

    private static readonly string[] SensitiveKeywords =
        ["password", "secret", "token", "apikey", "key", "connectionstring", "securitystamp"];

    public AuditableEntityInterceptor(ILogger<AuditableEntityInterceptor>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditableEntityInterceptor>.Instance;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditFieldsAndCaptureDiffs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFieldsAndCaptureDiffs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFieldsAndCaptureDiffs(DbContext? context)
    {
        if (context == null) return;

        var appDb = context as AppDbContext;
        var currentUser = appDb?.CurrentUser ?? new DefaultCurrentUser();
        var tenantContext = appDb?.TenantContext ?? new TenantContext();
        var dateTimeProvider = appDb?.DateTimeProvider ?? new ConfigurableDateTimeProvider(true);

        // Check if there are non-exempt entities being modified
        var hasAuditableEntries = context.ChangeTracker.Entries()
            .Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                      && e.Entity is not DataProtectionKey 
                      && e.Entity is not AuditRecordEntity);

        if (!hasAuditableEntries) return;

        // Zero-Trust security: any business entity modification strictly requires an authenticated user or daemon
        if (!currentUser.IsAuthenticated)
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Entity modifications require an established authenticated user or system daemon context.");
        }

        var now = dateTimeProvider.Now.Kind == DateTimeKind.Utc
            ? dateTimeProvider.Now
            : DateTime.SpecifyKind(dateTimeProvider.Now, DateTimeKind.Utc);

        var baseUserId = SystemIdentityUtility.ResolveAuditableUserId(currentUser, "SaveChanges");
        var currentUserId = currentUser.IsImpersonated
            ? $"{baseUserId} [Impersonated by {currentUser.ImpersonatedBy}]"
            : baseUserId;

        // 1. Stamp Level 1 audit properties & enforce tenant isolation
        UpdateAuditFields(context, now, currentUserId, tenantContext);

        // 2. Capture Level 2 property diffs and append AuditRecordEntity to the same transaction
        CaptureEntityDiffs(context, now, currentUserId, currentUser, tenantContext);
    }

    private void UpdateAuditFields(DbContext context, DateTime now, string currentUserId, ITenantContext tenantContext)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is DataProtectionKey || entry.Entity is AuditRecordEntity)
            {
                continue;
            }

            // ─── 0. AUTOMATIC SOFT-DELETE CONVERSION ─────────────────────────────────
            // If code calls context.Remove(entity) on an ISoftDeletableEntity,
            // intercept the hard delete and convert it into a soft delete.
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletableEntity softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAtUtc = now;
                softDeletable.DeletedBy = currentUserId;
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
                        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
                        {
                            _logger.LogError(
                                "Tenant isolation error: Cannot save entity '{EntityType}' because TenantId is not set and no active tenant is available in ITenantContext",
                                entry.Entity.GetType().Name);

                            // Fail-closed: never persist an entity without a TenantId
                            throw new InvalidOperationException(
                                $"Cannot save entity '{entry.Entity.GetType().Name}': " +
                                $"TenantId is not set and no active tenant is available in ITenantContext. " +
                                $"Ensure the user session is fully initialized before writing tenant-scoped data.");
                        }

                        tenantEntity.TenantId = tenantContext.TenantId;
                    }
                    else if (!tenantContext.IsHost &&
                             tenantEntity.TenantId != tenantContext.TenantId)
                    {
                        _logger.LogWarning(
                            "Security violation: attempt to insert entity '{EntityType}' into tenant '{TargetTenant}' while active tenant is '{ActiveTenant}'",
                            entry.Entity.GetType().Name, tenantEntity.TenantId, tenantContext.TenantId);

                        // Security: a non-host user is trying to write to a different tenant's data.
                        throw new InvalidOperationException(
                            $"Security violation: attempt to insert entity '{entry.Entity.GetType().Name}' " +
                            $"into tenant '{tenantEntity.TenantId}' while active tenant is '{tenantContext.TenantId}'.");
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    // TenantId is immutable — it can never be changed after creation.
                    entry.Property(TenantIdProperty).IsModified = false;

                    // Cross-project task immobility: Tasks can NEVER be moved between projects
                    if (entry.Entity is UserTaskEntity)
                    {
                        entry.Property(nameof(UserTaskEntity.ProjectId)).IsModified = false;
                    }

                    // Cross-tenant write guard for non-host users (check both original and current values)
                    var originalTenantId = entry.Property(TenantIdProperty).OriginalValue as string;
                    if (!tenantContext.IsHost &&
                        ((!string.IsNullOrEmpty(originalTenantId) && originalTenantId != tenantContext.TenantId) ||
                         tenantEntity.TenantId != tenantContext.TenantId))
                    {
                        var violatingTenant = originalTenantId ?? tenantEntity.TenantId;
                        _logger.LogWarning(
                            "Security violation: attempt to modify entity '{EntityType}' belonging to tenant '{TargetTenant}' while active tenant is '{ActiveTenant}'",
                            entry.Entity.GetType().Name, violatingTenant, tenantContext.TenantId);

                        throw new InvalidOperationException(
                            $"Security violation: attempt to modify entity '{entry.Entity.GetType().Name}' " +
                            $"belonging to tenant '{violatingTenant}' while active tenant is '{tenantContext.TenantId}'.");
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    // Cross-tenant delete guard for hard deletes
                    var originalTenantId = entry.Property(TenantIdProperty).OriginalValue as string ?? tenantEntity.TenantId;
                    if (!tenantContext.IsHost && originalTenantId != tenantContext.TenantId)
                    {
                        _logger.LogWarning(
                            "Security violation: attempt to delete entity '{EntityType}' belonging to tenant '{TargetTenant}' while active tenant is '{ActiveTenant}'",
                            entry.Entity.GetType().Name, originalTenantId, tenantContext.TenantId);

                        throw new InvalidOperationException(
                            $"Security violation: attempt to delete entity '{entry.Entity.GetType().Name}' " +
                            $"belonging to tenant '{originalTenantId}' while active tenant is '{tenantContext.TenantId}'.");
                    }
                }
            }
            // ─────────────────────────────────────────────────────────────────────────

            // Tenant Slug is strictly immutable — can never be modified after creation
            if (entry.Entity is TenantEntity && entry.State == EntityState.Modified)
            {
                entry.Property(nameof(TenantEntity.Slug)).IsModified = false;
            }

            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
            {
                continue;
            }

            // ─── AUDIT FIELDS ─────────────────────────────────────────────────────────
            // 1. Strongly typed IAuditableEntity
            if (entry.Entity is IAuditableEntity auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    auditableEntity.Created = now;
                    auditableEntity.CreatedBy = currentUserId;
                    auditableEntity.Updated = now;
                    auditableEntity.UpdatedBy = currentUserId;
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
                    if (hasUpdated) entry.Property(UpdatedProperty).CurrentValue = now;
                    if (hasUpdatedBy) entry.Property(UpdatedByProperty).CurrentValue = currentUserId;
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

            // ─── VERSIONING ───────────────────────────────────────────────────────────
            if (entry.Entity is IVersionedEntity versionedEntity)
            {
                if (entry.State == EntityState.Added && versionedEntity.VerNum <= 0)
                {
                    versionedEntity.VerNum = 1;
                }
                // On Modified, version updates are explicitly governed by entity.SetVersionNumber(newVersion)
            }

            // ─── EFFECTIVE DATING VALIDATION ──────────────────────────────────────────
            if (entry.Entity is IEffectiveDatedEntity effectiveDated)
            {
                if (effectiveDated.EndDate.HasValue && effectiveDated.EndDate.Value < effectiveDated.StartDate)
                {
                    throw new InvalidOperationException(
                        $"Entity '{entry.Entity.GetType().Name}' has an invalid effective date range: " +
                        $"EndDate ({effectiveDated.EndDate.Value:u}) cannot precede StartDate ({effectiveDated.StartDate:u}).");
                }
            }
        }
    }

    private void CaptureEntityDiffs(DbContext context, DateTime now, string currentUserId, ICurrentUser currentUser, ITenantContext tenantContext)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                        && e.Entity is not IAuditExemptEntity
                        && e.Entity is not DataProtectionKey)
            .ToList();

        if (entries.Count == 0) return;

        var auditRecords = new List<AuditRecordEntity>();

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

            var operation = entry.State switch
            {
                EntityState.Added => EntityOperation.Insert,
                EntityState.Modified => EntityOperation.Update,
                EntityState.Deleted => EntityOperation.Delete,
                _ => (EntityOperation?)null
            };

            if (operation == null) continue;

            var changes = new Dictionary<string, object?>();

            if (operation == EntityOperation.Update)
            {
                foreach (var prop in entry.Properties)
                {
                    if (!prop.IsModified) continue;
                    var name = prop.Metadata.Name;
                    if (name is CreatedProperty or CreatedByProperty or UpdatedProperty or UpdatedByProperty or TenantIdProperty) continue;

                    var isSensitive = IsSensitiveProperty(name);
                    var oldVal = isSensitive ? (prop.OriginalValue != null ? "****" : null) : prop.OriginalValue;
                    var newVal = isSensitive ? (prop.CurrentValue != null ? "****" : null) : prop.CurrentValue;

                    changes[name] = new { Old = oldVal, New = newVal };
                }
            }
            else if (operation == EntityOperation.Insert)
            {
                foreach (var prop in entry.Properties)
                {
                    var name = prop.Metadata.Name;
                    if (name is CreatedProperty or CreatedByProperty or UpdatedProperty or UpdatedByProperty) continue;
                    if (prop.CurrentValue == null) continue;

                    var isSensitive = IsSensitiveProperty(name);
                    changes[name] = isSensitive ? "****" : prop.CurrentValue;
                }
            }
            else if (operation == EntityOperation.Delete)
            {
                foreach (var prop in entry.Properties)
                {
                    var name = prop.Metadata.Name;
                    if (name is CreatedProperty or CreatedByProperty or UpdatedProperty or UpdatedByProperty) continue;

                    var isSensitive = IsSensitiveProperty(name);
                    changes[name] = isSensitive ? "****" : prop.OriginalValue;
                }
            }

            var tenantId = (entry.Entity as ITenantEntity)?.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = tenantContext.TenantId;
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new InvalidOperationException(
                    $"Zero-Trust Security Violation: Cannot record entity audit for '{entityName}'. Active tenant context is missing.");
            }

            var record = new AuditRecordEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                UserId = currentUserId,
                UserEmail = currentUser.Email,
                UserType = tenantContext.UserType,
                EventType = AuditEventType.EntityChange,
                Severity = AuditSeverity.Information,
                EntityName = entityName,
                EntityId = entityId,
                Operation = operation,
                ChangesJson = changes.Count > 0 ? JsonSerializer.Serialize(changes) : null,
                Created = now,
                CreatedBy = currentUserId,
                Updated = now,
                UpdatedBy = currentUserId
            };

            auditRecords.Add(record);

            // Forensic Oversight: Audit Host user exercising cross-tenant modification privilege
            if (tenantContext.IsHost &&
                !string.IsNullOrWhiteSpace(tenantContext.TenantId) &&
                !string.Equals(tenantId, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                var crossTenantSecurityAudit = new AuditRecordEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenantId,
                    UserId = currentUserId,
                    UserEmail = currentUser.Email,
                    UserType = tenantContext.UserType,
                    EventType = AuditEventType.Security,
                    Severity = AuditSeverity.Warning,
                    EntityName = entityName,
                    EntityId = entityId,
                    Operation = operation,
                    Description = $"[Host Oversight] Host user '{currentUserId}' performed cross-tenant {operation} on entity '{entityName}' (ID: {entityId}) in tenant '{tenantId}'.",
                    Created = now,
                    CreatedBy = currentUserId,
                    Updated = now,
                    UpdatedBy = currentUserId
                };
                auditRecords.Add(crossTenantSecurityAudit);
            }
        }

        if (auditRecords.Count > 0)
        {
            context.Set<AuditRecordEntity>().AddRange(auditRecords);
            _logger.LogDebug(
                "Captured {AuditRecordCount} Level-2 entity audit record(s) for Tenant '{TenantId}'",
                auditRecords.Count, tenantContext.TenantId);
        }
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        var lower = propertyName.ToLowerInvariant();
        return SensitiveKeywords.Any(k => lower.Contains(k));
    }
}
