using System.Collections.Concurrent;
using BlazorEnterprise.Core.Exceptions;
using BlazorEnterprise.Persistence.Context;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorEnterprise.Persistence.Interceptors;

/// <summary>
/// Singleton EF Core SaveChanges interceptor enforcing Tier 2 entity invariant validations.
/// Executes before SQL generation and before AuditableEntityInterceptor.
/// Uses static ConcurrentDictionary caching for sub-microsecond DI validator resolution.
/// Aggregates all validation failures across all entities in the commit batch into an EntityValidationException.
/// </summary>
public class EntityValidationInterceptor : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, Type?> _validatorTypeCache = new();
    private readonly IServiceProvider? _fallbackServiceProvider;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public EntityValidationInterceptor()
    {
        _fallbackServiceProvider = null;
    }

    public EntityValidationInterceptor(IServiceProvider fallbackServiceProvider)
    {
        _fallbackServiceProvider = fallbackServiceProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ValidateEntitiesSync(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await ValidateEntitiesAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task ValidateEntitiesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        if (entries.Count == 0) return;

        var errors = new Dictionary<string, List<string>>();

        var sp = (context as AppDbContext)?.ServiceProvider ?? _fallbackServiceProvider;
        if (sp is null) return;

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType();
            // Hierarchy Scoping Invariants Verification
            if (entry.Entity is BlazorEnterprise.Core.Domain.Base.TenantScopedEntity tenantScoped)
            {
                if (tenantScoped.TenantId == Guid.Empty)
                {
                    var entityName = entityType.Name;
                    if (!errors.ContainsKey(entityName)) errors[entityName] = new();
                    errors[entityName].Add("TenantId must not be empty.");
                }
                if (entry.Entity is BlazorEnterprise.Core.Domain.Base.ProgramScopedEntity progScoped)
                {
                    var projScoped = entry.Entity as BlazorEnterprise.Core.Domain.Base.ProjectScopedEntity;
                    var projectId = projScoped?.ProjectId;
                    var programId = progScoped.ProgramId;

                    switch (tenantScoped.Level)
                    {
                        case BlazorEnterprise.Shared.DataListTypes.TaskLevelEnum.Tenant:
                            if (programId.HasValue || projectId.HasValue)
                            {
                                var entityName = entityType.Name;
                                if (!errors.ContainsKey(entityName)) errors[entityName] = new();
                                errors[entityName].Add("Tenant-level entities must have null ProgramId and ProjectId.");
                            }
                            break;
                        case BlazorEnterprise.Shared.DataListTypes.TaskLevelEnum.Program:
                            if (!programId.HasValue || programId.Value == Guid.Empty || projectId.HasValue)
                            {
                                var entityName = entityType.Name;
                                if (!errors.ContainsKey(entityName)) errors[entityName] = new();
                                errors[entityName].Add("Program-level entities must have a valid ProgramId and null ProjectId.");
                            }
                            break;
                        case BlazorEnterprise.Shared.DataListTypes.TaskLevelEnum.Project:
                            if (!projectId.HasValue || projectId.Value == Guid.Empty)
                            {
                                var entityName = entityType.Name;
                                if (!errors.ContainsKey(entityName)) errors[entityName] = new();
                                errors[entityName].Add("Project-level entities must have a valid ProjectId.");
                            }
                            break;
                    }
                }
            }

            if (entry.Entity is BlazorEnterprise.Core.Domain.Auditing.AuditRecordEntity audit)
            {
                if (audit.RetentionDays == 0 || !Enum.IsDefined(audit.RetentionDays))
                {
                    audit.RetentionDays = audit.EventType == BlazorEnterprise.Shared.DataListTypes.AuditEventTypeEnum.Security
                        ? BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.ThreeHundredSixtyFiveDays
                        : BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.NinetyDays;
                }
                else if (audit.EventType == BlazorEnterprise.Shared.DataListTypes.AuditEventTypeEnum.Security && audit.RetentionDays == BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.NinetyDays)
                {
                    audit.RetentionDays = BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.ThreeHundredSixtyFiveDays;
                }
            }

            var validatorType = _validatorTypeCache.GetOrAdd(
                entityType,
                t => typeof(IValidator<>).MakeGenericType(t));

            if (validatorType is null) continue;

            if (sp.GetService(validatorType) is IValidator validator)
            {
                var validationContext = new ValidationContext<object>(entry.Entity);
                var validationResult = await validator.ValidateAsync(validationContext, cancellationToken);

                if (!validationResult.IsValid)
                {
                    var entityName = entityType.Name;
                    foreach (var failure in validationResult.Errors)
                    {
                        var key = $"{entityName}.{failure.PropertyName}";
                        if (!errors.TryGetValue(key, out var list))
                        {
                            list = new List<string>();
                            errors[key] = list;
                        }
                        list.Add(failure.ErrorMessage);
                    }
                }
            }
            else
            {
                // Fail-closed safety: Entity has no registered validator in DI
                var entityName = entityType.Name;
                var key = $"{entityName}.Validator";
                if (!errors.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    errors[key] = list;
                }
                list.Add($"Entity '{entityName}' lacks a registered IValidator<{entityName}>.");
            }
        }

        if (errors.Count > 0)
        {
            throw new EntityValidationException(errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
        }
    }

    private void ValidateEntitiesSync(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        if (entries.Count == 0) return;

        var errors = new Dictionary<string, List<string>>();

        var sp = (context as AppDbContext)?.ServiceProvider ?? _fallbackServiceProvider;
        if (sp is null) return;

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType();
            if (entry.Entity is BlazorEnterprise.Core.Domain.Auditing.AuditRecordEntity audit)
            {
                if (audit.RetentionDays == 0 || !Enum.IsDefined(audit.RetentionDays))
                {
                    audit.RetentionDays = audit.EventType == BlazorEnterprise.Shared.DataListTypes.AuditEventTypeEnum.Security
                        ? BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.ThreeHundredSixtyFiveDays
                        : BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.NinetyDays;
                }
                else if (audit.EventType == BlazorEnterprise.Shared.DataListTypes.AuditEventTypeEnum.Security && audit.RetentionDays == BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.NinetyDays)
                {
                    audit.RetentionDays = BlazorEnterprise.Shared.DataListTypes.AuditRetentionPeriodEnum.ThreeHundredSixtyFiveDays;
                }
            }

            var validatorType = _validatorTypeCache.GetOrAdd(
                entityType,
                t => typeof(IValidator<>).MakeGenericType(t));

            if (validatorType is null) continue;

            if (sp.GetService(validatorType) is IValidator validator)
            {
                var validationContext = new ValidationContext<object>(entry.Entity);
                var validationResult = validator.Validate(validationContext);

                if (!validationResult.IsValid)
                {
                    var entityName = entityType.Name;
                    foreach (var failure in validationResult.Errors)
                    {
                        var key = $"{entityName}.{failure.PropertyName}";
                        if (!errors.TryGetValue(key, out var list))
                        {
                            list = new List<string>();
                            errors[key] = list;
                        }
                        list.Add(failure.ErrorMessage);
                    }
                }
            }
            else
            {
                var entityName = entityType.Name;
                var key = $"{entityName}.Validator";
                if (!errors.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    errors[key] = list;
                }
                list.Add($"Entity '{entityName}' lacks a registered IValidator<{entityName}>.");
            }
        }

        if (errors.Count > 0)
        {
            throw new EntityValidationException(errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
        }
    }
}
