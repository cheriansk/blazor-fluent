using System.Reflection;
using BlazorFluent.Core.Domain.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// Fail-closed EF Core Interceptor that guarantees data consistency under a NoTracking-by-default architecture.
/// 
/// How it works:
/// 1. Intercepts entity materialization (<see cref="IMaterializationInterceptor"/>) and snapshots scalar properties of entities.
/// 2. On <see cref="SaveChanges"/> (<see cref="ISaveChangesInterceptor"/>), verifies whether any entity modified in memory 
///    was left detached from the <see cref="ChangeTracker"/>.
/// 3. If an entity was modified in memory without being tracked (missing .AsTracking() on query or missing dbContext.Update(entity)),
///    it throws an immediate <see cref="InvalidOperationException"/> identifying the entity, ID, and changed property.
/// </summary>
public class NoTrackingMutationGuardInterceptor : SaveChangesInterceptor, IMaterializationInterceptor
{
    private readonly ILogger<NoTrackingMutationGuardInterceptor> _logger;
    private readonly List<NoTrackingSnapshotEntry> _untrackedSnapshots = new();
    private readonly object _lock = new();

    public NoTrackingMutationGuardInterceptor(ILogger<NoTrackingMutationGuardInterceptor>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NoTrackingMutationGuardInterceptor>.Instance;
    }

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is BaseEntity baseEntity && materializationData.Context != null)
        {
            lock (_lock)
            {
                // Prune dead references to keep memory minimal
                _untrackedSnapshots.RemoveAll(s => !s.WeakRef.TryGetTarget(out _));

                var snapshot = CapturePropertySnapshot(baseEntity);
                _untrackedSnapshots.Add(new NoTrackingSnapshotEntry(
                    new WeakReference<BaseEntity>(baseEntity),
                    baseEntity.GetType(),
                    baseEntity.Id,
                    snapshot));
            }
        }

        return entity;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ValidateUntrackedMutations(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateUntrackedMutations(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ValidateUntrackedMutations(DbContext? context)
    {
        if (context == null) return;

        lock (_lock)
        {
            foreach (var entry in _untrackedSnapshots)
            {
                if (entry.WeakRef.TryGetTarget(out var liveEntity))
                {
                    // If the entity is currently tracked in ChangeTracker, it is legitimate (.AsTracking() or dbContext.Update())
                    var trackerEntry = context.ChangeTracker.Entries()
                        .FirstOrDefault(e => ReferenceEquals(e.Entity, liveEntity));

                    if (trackerEntry != null && trackerEntry.State != EntityState.Detached)
                    {
                        continue;
                    }

                    // Entity is detached / untracked in this context. Inspect if any scalar property was modified in memory!
                    var currentProps = CapturePropertySnapshot(liveEntity);
                    foreach (var (propName, originalVal) in entry.OriginalValues)
                    {
                        if (currentProps.TryGetValue(propName, out var currentVal))
                        {
                            if (!Equals(originalVal, currentVal))
                            {
                                var entityName = entry.EntityType.Name;
                                var entityId = entry.EntityId;
                                var message =
                                    $"Fail-Closed Architecture Violation: Entity '{entityName}' (ID: '{entityId}') was queried with NoTracking " +
                                    $"and had property '{propName}' modified in memory (Original: '{originalVal}', Current: '{currentVal}'), " +
                                    $"but SaveChanges was called without tracking it. Under the NoTracking architecture, you MUST query with .AsTracking() " +
                                    $"or attach the entity using dbContext.Update(entity) before calling SaveChanges.";

                                _logger.LogCritical(message);
                                throw new InvalidOperationException(message);
                            }
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<string, object?> CapturePropertySnapshot(BaseEntity entity)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        var type = entity.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (!prop.CanRead) continue;

            var propType = prop.PropertyType;
            // Only snapshot scalar/value types and strings, skip navigations and collections
            if (propType.IsPrimitive ||
                propType.IsEnum ||
                propType == typeof(string) ||
                propType == typeof(Guid) ||
                propType == typeof(DateTime) ||
                propType == typeof(DateTimeOffset) ||
                propType == typeof(TimeSpan) ||
                Nullable.GetUnderlyingType(propType) != null)
            {
                try
                {
                    dict[prop.Name] = prop.GetValue(entity);
                }
                catch
                {
                    // Ignore throwing getters
                }
            }
        }

        return dict;
    }

    private sealed record NoTrackingSnapshotEntry(
        WeakReference<BaseEntity> WeakRef,
        Type EntityType,
        Guid EntityId,
        Dictionary<string, object?> OriginalValues);
}
