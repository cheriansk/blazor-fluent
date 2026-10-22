using System.Security;
using System.Text.Json;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Events;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Utilities;
using BlazorFluent.Persistence.Context;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger<AuditableEntityInterceptor> _logger;

    public const string CreatedProperty = "Created";
    public const string CreatedByProperty = "CreatedBy";
    public const string UpdatedProperty = "Updated";
    public const string UpdatedByProperty = "UpdatedBy";
    public const string TenantIdProperty = "TenantId";

    private static readonly string[] SensitiveKeywords =
        ["password", "secret", "token", "apikey", "key", "connectionstring", "securitystamp"];

    public AuditableEntityInterceptor(
        IConfiguration? configuration = null,
        ILogger<AuditableEntityInterceptor>? logger = null)
    {
        _configuration = configuration;
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
        UpdateAuditFields(context, now, currentUserId, tenantContext, currentUser);

        // 2. Capture Level 2 property diffs and append AuditRecordEntity to the same transaction
        CaptureEntityDiffs(context, now, currentUserId, currentUser, tenantContext);
    }

    private void UpdateAuditFields(DbContext context, DateTime now, string currentUserId, ITenantContext tenantContext, ICurrentUser currentUser)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            // ─── 0. IMMUTABLE RECORD SECURITY GUARD ─────────────────────────────────
            // Guard immutable records (AuditRecordEntity, EventTrackers, JobExecutions) from deletion and mutation
            if (entry.Entity is IImmutableEntity)
            {
                if (entry.State == EntityState.Deleted)
                {
                    throw new InvalidOperationException(
                        $"Security violation: Entity '{entry.Entity.GetType().Name}' is an immutable ledger record ({nameof(IImmutableEntity)}) and can NEVER be deleted.");
                }

                if (entry.State == EntityState.Modified && entry.Entity is AuditRecordEntity or EventPublishTrackerEntity or EventConsumptionTrackerEntity)
                {
                    throw new InvalidOperationException(
                        $"Security violation: Entity '{entry.Entity.GetType().Name}' is an immutable ledger record ({nameof(IImmutableEntity)}) and can NEVER be modified after creation.");
                }
            }

            if (entry.Entity is DataProtectionKey || entry.Entity is AuditRecordEntity)
            {
                continue;
            }

            // ─── 0a. SYSTEM DAEMON RESTRICTION GUARD ────────────────────────────────
            // SystemDaemon can NEVER perform administrative operations: creating/modifying/deleting
            // Tenants, Users, or Role assignments (TenantUser, ProgramUserRole, ProjectUserRole).
            // Daemons are restricted to pure data processing only.
            if (currentUser.IsSystemDaemon || SystemIdentityUtility.IsSystemDaemon(currentUser.UserId))
            {
                if (entry.Entity is TenantEntity or UserEntity or TenantUserEntity or ProgramUserRoleEntity or ProjectUserRoleEntity)
                {
                    _logger.LogCritical(
                        "CRITICAL SECURITY VIOLATION: System daemon '{DaemonId}' attempted {State} on administrative entity '{EntityType}'. System daemons are strictly restricted to data processing only.",
                        currentUser.UserId, entry.State, entry.Entity.GetType().Name);

                    throw new SecurityException(
                        $"CRITICAL SECURITY VIOLATION: System daemon '{currentUser.UserId}' is strictly restricted to data processing only and cannot modify administrative entity '{entry.Entity.GetType().Name}'.");
                }
            }

            // ─── 0b. TENANT ADMIN SEPARATION OF DUTIES GUARD ─────────────────────────
            // Only Root Admins operating within the Root Tenant can assign, modify, or revoke TenantAdmin role
            if (entry.Entity is TenantUserEntity tu && entry.State is EntityState.Added or EntityState.Modified)
            {
                if (tu.Role == TenantRoleEnum.TenantAdmin && (!currentUser.IsRootAdmin || !tenantContext.IsRootTenant))
                {
                    _logger.LogCritical(
                        "CRITICAL SECURITY VIOLATION: Caller '{Caller}' (Email: '{Email}') attempted to assign or update the TenantAdmin role on TenantUserEntity {EntityId}. Only Root Admins operating within the Root Tenant can assign Tenant Admin privileges.",
                        currentUserId, currentUser.Email, tu.Id);

                    throw new SecurityException(
                        $"Zero-Trust Security Violation: Caller '{currentUser.Email}' attempted to assign or update the TenantAdmin role on TenantUserEntity {tu.Id}. Only Root Admins operating within the Root Tenant can assign Tenant Admin privileges.");
                }
            }

            // ─── 0c. CRYPTOGRAPHIC HMAC ROW-INTEGRITY SEALING ────────────────────────
            // Automatically recompute HMAC-SHA256 signature for all IHmacSignableEntity records on insert or update
            if (entry.Entity is IHmacSignableEntity hmacSignable && entry.State is EntityState.Added or EntityState.Modified)
            {
                var secret = _configuration?["Security:IntegritySecret"];
                if (!string.IsNullOrWhiteSpace(secret) && !secret.StartsWith("__SET_VIA_"))
                {
                    hmacSignable.RowSignature = hmacSignable.ComputeIntegritySignature(secret);
                }
            }

            // ─── 1. AUTOMATIC SOFT-DELETE CONVERSION ─────────────────────────────────
            // If code calls context.Remove(entity) on an ISoftDeletableEntity,
            // intercept the hard delete and convert it into a soft delete.
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletableEntity softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAtUtc = now;
                softDeletable.DeletedBy = currentUserId;
            }

            // ─── 2. HARD-DELETE CONFORMANCE ──────────────────────────────────────────
            // If entry.Entity is IHardDeletableEntity, entry.State remains EntityState.Deleted,
            // proceeding to physical SQL DELETE in PostgreSQL.

            // ─── TENANT SECURITY ──────────────────────────────────────────────────────
            if (entry.Entity is ITenantEntity tenantEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    // If TenantId was not set by application code, inject the current context's tenant.
                    if (tenantEntity.TenantId == Guid.Empty)
                    {
                        if (!tenantContext.HasActiveTenant || tenantContext.TenantId == Guid.Empty)
                        {
                            _logger.LogCritical(
                                "CRITICAL SECURITY VIOLATION: Cannot save entity '{EntityType}' because TenantId is not set and no active tenant is available in ITenantContext.",
                                entry.Entity.GetType().Name);

                            // Fail-closed: never persist an entity without a TenantId
                            throw new SecurityException(
                                $"Cannot save entity '{entry.Entity.GetType().Name}': TenantId is not set and no active tenant is available in ITenantContext. Ensure the user session is fully initialized before writing tenant-scoped data.");
                        }

                        tenantEntity.TenantId = tenantContext.TenantId;
                    }
                    else if (tenantContext.HasActiveTenant && tenantEntity.TenantId != tenantContext.TenantId)
                    {
                        // Narrow exception: Root Admin operating in Root Tenant assigning TenantAdmin role to customer tenant
                        var isRootAssigningTenantAdmin = tenantContext.IsRootTenant && currentUser.IsRootAdmin && entry.Entity is TenantUserEntity newTu && newTu.Role == TenantRoleEnum.TenantAdmin;
                        if (!isRootAssigningTenantAdmin)
                        {
                            _logger.LogCritical(
                                "CRITICAL SECURITY VIOLATION: attempt to insert entity '{EntityType}' into tenant '{TargetTenant}' while active tenant is '{ActiveTenant}'",
                                entry.Entity.GetType().Name, tenantEntity.TenantId, tenantContext.TenantId);

                            throw new SecurityException(
                                $"Security violation: attempt to insert entity '{entry.Entity.GetType().Name}' into tenant '{tenantEntity.TenantId}' while active tenant is '{tenantContext.TenantId}'.");
                        }
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    // ─── HARD IMMUTABILITY SECURITY BARRIER ──────────────────────────────────
                    // 1. TenantId is immutable across all tenant entities
                    if (entry.Property(TenantIdProperty).IsModified)
                    {
                        _logger.LogCritical(
                            "CRITICAL SECURITY ALERT: Caller '{Caller}' attempted to mutate immutable TenantId on '{Entity}' from '{Original}' to '{Current}'",
                            currentUserId, entry.Entity.GetType().Name, entry.Property(TenantIdProperty).OriginalValue, entry.Property(TenantIdProperty).CurrentValue);

                        throw new SecurityException(
                            $"Security Violation: 'TenantId' is immutable on '{entry.Entity.GetType().Name}' and cannot be altered once created.");
                    }

                    // 2. BaseTaskEntity Scoping Immutability: Level, ProgramId, and ProjectId can NEVER be moved once created
                    if (entry.Entity is BaseTaskEntity baseTask)
                    {
                        if (entry.Property(nameof(BaseTaskEntity.Level)).IsModified)
                        {
                            _logger.LogCritical(
                                "CRITICAL SECURITY ALERT: Caller '{Caller}' attempted to mutate immutable Level on '{Entity}' ({Id}) from '{Original}' to '{Current}'",
                                currentUserId, entry.Entity.GetType().Name, baseTask.Id, entry.Property(nameof(BaseTaskEntity.Level)).OriginalValue, baseTask.Level);

                            throw new SecurityException(
                                $"Security Violation: 'Level' is immutable on '{entry.Entity.GetType().Name}' and cannot be altered once created.");
                        }

                        if (entry.Property(nameof(BaseTaskEntity.ProgramId)).IsModified)
                        {
                            _logger.LogCritical(
                                "CRITICAL SECURITY ALERT: Caller '{Caller}' attempted to mutate immutable ProgramId on '{Entity}' ({Id}) from '{Original}' to '{Current}'",
                                currentUserId, entry.Entity.GetType().Name, baseTask.Id, entry.Property(nameof(BaseTaskEntity.ProgramId)).OriginalValue, baseTask.ProgramId);

                            throw new SecurityException(
                                $"Security Violation: 'ProgramId' is immutable on '{entry.Entity.GetType().Name}' and cannot be altered once created.");
                        }

                        if (entry.Property(nameof(BaseTaskEntity.ProjectId)).IsModified)
                        {
                            _logger.LogCritical(
                                "CRITICAL SECURITY ALERT: Caller '{Caller}' attempted to mutate immutable ProjectId on '{Entity}' ({Id}) from '{Original}' to '{Current}'",
                                currentUserId, entry.Entity.GetType().Name, baseTask.Id, entry.Property(nameof(BaseTaskEntity.ProjectId)).OriginalValue, baseTask.ProjectId);

                            throw new SecurityException(
                                $"Security Violation: 'ProjectId' is immutable on '{entry.Entity.GetType().Name}' and cannot be altered once created.");
                        }
                    }

                    // Cross-tenant write guard (check both original and current values)
                    var originalTenantId = (Guid)entry.Property(TenantIdProperty).OriginalValue;
                    if (tenantContext.HasActiveTenant &&
                        ((originalTenantId != Guid.Empty && originalTenantId != tenantContext.TenantId) ||
                         tenantEntity.TenantId != tenantContext.TenantId))
                    {
                        var isRootAssigningTenantAdmin = tenantContext.IsRootTenant && currentUser.IsRootAdmin && entry.Entity is TenantUserEntity;
                        if (!isRootAssigningTenantAdmin)
                        {
                            var violatingTenant = originalTenantId != Guid.Empty ? originalTenantId : tenantEntity.TenantId;
                            _logger.LogCritical(
                                "CRITICAL SECURITY VIOLATION: attempt to modify entity '{EntityType}' belonging to tenant '{TargetTenant}' while active tenant is '{ActiveTenant}'",
                                entry.Entity.GetType().Name, violatingTenant, tenantContext.TenantId);

                            throw new SecurityException(
                                $"Security violation: attempt to modify entity '{entry.Entity.GetType().Name}' belonging to tenant '{violatingTenant}' while active tenant is '{tenantContext.TenantId}'.");
                        }
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    // Cross-tenant delete guard for hard deletes
                    var originalTenantId = (Guid)entry.Property(TenantIdProperty).OriginalValue;
                    if (originalTenantId == Guid.Empty)
                    {
                        originalTenantId = tenantEntity.TenantId;
                    }
                    if (tenantContext.HasActiveTenant && originalTenantId != tenantContext.TenantId)
                    {
                        _logger.LogCritical(
                            "CRITICAL SECURITY VIOLATION: attempt to delete entity '{EntityType}' belonging to tenant '{TargetTenant}' while active tenant is '{ActiveTenant}'",
                            entry.Entity.GetType().Name, originalTenantId, tenantContext.TenantId);

                        throw new SecurityException(
                            $"Security violation: attempt to delete entity '{entry.Entity.GetType().Name}' belonging to tenant '{originalTenantId}' while active tenant is '{tenantContext.TenantId}'.");
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
                if (effectiveDated.EndDate.HasValue && effectiveDated.EndDate.Value.Date < effectiveDated.StartDate.Date)
                {
                    throw new InvalidOperationException(
                        $"Entity '{entry.Entity.GetType().Name}' has an invalid effective date range: " +
                        $"EndDate ({effectiveDated.EndDate.Value:yyyy-MM-dd}) cannot precede StartDate ({effectiveDated.StartDate:yyyy-MM-dd}).");
                }
            }

            // ─── SCHEDULED WORK AUTOMATION & GOVERNANCE ────────────────────────
            if (entry.Entity is IScheduledWorkEntity scheduledWork)
            {
                if (entry.Entity is BaseTaskEntity baseTask)
                {
                    if (baseTask.Status == TaskStatusEnum.InProgress && !baseTask.ActualStartDate.HasValue)
                    {
                        baseTask.ActualStartDate = now;
                    }
                    else if (baseTask.Status == TaskStatusEnum.Closed)
                    {
                        baseTask.ActualEndDate ??= now;
                        baseTask.EndDate ??= now;
                    }
                    else if (baseTask.Status is TaskStatusEnum.Open or TaskStatusEnum.InProgress)
                    {
                        var originalStatusProp = entry.Property(nameof(BaseTaskEntity.Status));
                        if (originalStatusProp.IsModified && originalStatusProp.OriginalValue is TaskStatusEnum oldStatus && oldStatus == TaskStatusEnum.Closed)
                        {
                            baseTask.ActualEndDate = null;
                        }
                    }
                }
                else if (entry.Entity is ProgramEntity program)
                {
                    if (program.Status == ProgramStatusEnum.Active && !program.ActualStartDate.HasValue)
                    {
                        program.ActualStartDate = now;
                    }
                    else if (program.Status == ProgramStatusEnum.Completed)
                    {
                        program.ActualEndDate ??= now;
                        program.EndDate ??= now;
                        program.ClosedAtUtc ??= now;
                    }
                    else if (program.Status is ProgramStatusEnum.Planning or ProgramStatusEnum.Active)
                    {
                        var origStatus = entry.Property(nameof(ProgramEntity.Status));
                        if (origStatus.IsModified && origStatus.OriginalValue is ProgramStatusEnum oldProgStatus && oldProgStatus == ProgramStatusEnum.Completed)
                        {
                            program.ActualEndDate = null;
                            program.ClosedAtUtc = null;
                        }
                    }
                }
                else if (entry.Entity is ProjectEntity project)
                {
                    if (project.Status == ProjectStatusEnum.Active && !project.ActualStartDate.HasValue)
                    {
                        project.ActualStartDate = now;
                    }
                    else if (project.Status == ProjectStatusEnum.Done)
                    {
                        project.ActualEndDate ??= now;
                        project.EndDate ??= now;
                    }
                    else if (project.Status is ProjectStatusEnum.New or ProjectStatusEnum.Active)
                    {
                        var origStatus = entry.Property(nameof(ProjectEntity.Status));
                        if (origStatus.IsModified && origStatus.OriginalValue is ProjectStatusEnum oldProjStatus && oldProjStatus == ProjectStatusEnum.Done)
                        {
                            project.ActualEndDate = null;
                        }
                    }
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
                EntityState.Added => EntityOperationEnum.Insert,
                EntityState.Modified => EntityOperationEnum.Update,
                EntityState.Deleted => EntityOperationEnum.Delete,
                _ => (EntityOperationEnum?)null
            };

            if (operation == null) continue;

            var changes = new Dictionary<string, object?>();

            if (operation == EntityOperationEnum.Update)
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
            else if (operation == EntityOperationEnum.Insert)
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
            else if (operation == EntityOperationEnum.Delete)
            {
                foreach (var prop in entry.Properties)
                {
                    var name = prop.Metadata.Name;
                    if (name is CreatedProperty or CreatedByProperty or UpdatedProperty or UpdatedByProperty) continue;

                    var isSensitive = IsSensitiveProperty(name);
                    changes[name] = isSensitive ? "****" : prop.OriginalValue;
                }
            }

            var tenantId = (entry.Entity as ITenantEntity)?.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty && tenantContext.HasActiveTenant)
            {
                tenantId = tenantContext.TenantId;
            }

            if (tenantId == Guid.Empty)
            {
                _logger.LogCritical("CRITICAL SECURITY VIOLATION: Cannot record entity audit for '{EntityName}'. Active tenant context is missing.", entityName);
                throw new SecurityException(
                    $"Zero-Trust Security Violation: Cannot record entity audit for '{entityName}'. Active tenant context is missing.");
            }

            var userEmail = !string.IsNullOrWhiteSpace(currentUser.Email) ? currentUser.Email : currentUserId;

            var record = new AuditRecordEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                UserId = currentUserId,
                UserEmail = userEmail,
                UserType = tenantContext.UserType,
                EventType = AuditEventTypeEnum.EntityChange,
                Severity = AuditSeverityEnum.Information,
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
