using System.Collections.Concurrent;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor enforcing Tier 2 entity invariant validations.
/// Executes before SQL generation and before AuditableEntityInterceptor.
/// Uses static ConcurrentDictionary caching for sub-microsecond DI validator resolution.
/// Aggregates all validation failures across all entities in the commit batch into an EntityValidationException.
/// </summary>
public class EntityValidationInterceptor : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, Type?> _validatorTypeCache = new();
    private readonly IServiceProvider _serviceProvider;

    public EntityValidationInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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

        foreach (var entry in entries)
        {
            if (entry.Entity is IValidationExemptEntity)
            {
                continue;
            }

            var entityType = entry.Entity.GetType();
            var validatorType = _validatorTypeCache.GetOrAdd(
                entityType,
                t => typeof(IValidator<>).MakeGenericType(t));

            if (validatorType is null) continue;

            if (_serviceProvider.GetService(validatorType) is IValidator validator)
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
                // Fail-closed safety: Non-exempt entity has no registered validator in DI
                var entityName = entityType.Name;
                var key = $"{entityName}.Validator";
                if (!errors.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    errors[key] = list;
                }
                list.Add($"Entity '{entityName}' does not implement IValidationExemptEntity and lacks a registered IValidator<{entityName}>.");
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

        foreach (var entry in entries)
        {
            if (entry.Entity is IValidationExemptEntity)
            {
                continue;
            }

            var entityType = entry.Entity.GetType();
            var validatorType = _validatorTypeCache.GetOrAdd(
                entityType,
                t => typeof(IValidator<>).MakeGenericType(t));

            if (validatorType is null) continue;

            if (_serviceProvider.GetService(validatorType) is IValidator validator)
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
                list.Add($"Entity '{entityName}' does not implement IValidationExemptEntity and lacks a registered IValidator<{entityName}>.");
            }
        }

        if (errors.Count > 0)
        {
            throw new EntityValidationException(errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
        }
    }
}
