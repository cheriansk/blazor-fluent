using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Events;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Managed transaction scope implementing ITransactionScope.
/// Coordinates in-memory staging, concurrency verification (PostgreSQL xmin / BaseEntity.Version),
/// pre/post-commit hooks, domain event harvesting, and fail-closed atomic commit/rollback.
/// </summary>
public sealed class TransactionContext : ITransactionScope
{
    private readonly AppDbContext _context;
    private readonly IDbContextTransaction? _dbTransaction;
    private readonly bool _isRootScope;
    private readonly List<Func<ITransactionScope, CancellationToken, Task>> _beforeCommitHooks = [];
    private readonly List<Func<CancellationToken, Task>> _afterCommitHooks = [];
    private readonly List<IDomainEvent> _scopeDomainEvents = [];
    private readonly HashSet<string> _affectedEntityDescriptors = [];
    private readonly HashSet<string> _synthesizedEventKeys = [];
    private bool _isDisposed;

#pragma warning disable TG025
    public TransactionContext(
        AppDbContext context,
        IDbContextTransaction? dbTransaction,
        bool isRootScope,
        string operationName)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbTransaction = dbTransaction;
        _isRootScope = isRootScope;
        ScopeId = Guid.NewGuid();
        OperationName = string.IsNullOrWhiteSpace(operationName) ? "AmbientOperation" : operationName;
    }

    public Guid ScopeId { get; }
    public string OperationName { get; }
    public bool IsRollbackOnly { get; private set; }
    public AppDbContext DbContext => _context;
    public bool IsRootScope => _isRootScope;

    public bool HasStagedChanges => _context.ChangeTracker.HasChanges();

    public void MarkRollbackOnly()
    {
        IsRollbackOnly = true;
    }

    public void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _scopeDomainEvents.Add(domainEvent);
    }

    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => _scopeDomainEvents.AsReadOnly();

    public TEntity StageInsert<TEntity>(TEntity entity) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _context.Set<TEntity>().Add(entity);
        }
        else
        {
            entry.State = EntityState.Added;
        }
        return entity;
    }

    public void StageInsertRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        foreach (var entity in entities)
        {
            StageInsert(entity);
        }
    }

    public TEntity StageUpdate<TEntity>(TEntity entity, uint? expectedVersion = null) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _context.Set<TEntity>().Attach(entity);
            entry.State = EntityState.Modified;
        }
        else if (entry.State == EntityState.Unchanged)
        {
            entry.State = EntityState.Modified;
        }

        if (expectedVersion.HasValue && expectedVersion.Value > 0 && entity is BaseEntity baseEntity)
        {
            if (baseEntity.Version > 0 && baseEntity.Version != expectedVersion.Value)
            {
                throw new DbUpdateConcurrencyException(
                    $"Concurrency Conflict on {typeof(TEntity).Name} ({baseEntity.Id}): " +
                    $"Expected version was {expectedVersion.Value}, but in-memory/tracked version is {baseEntity.Version}. " +
                    "Another user or batch operation has modified this record.");
            }

            var prop = entry.Property(nameof(BaseEntity.Version));
            prop.OriginalValue = expectedVersion.Value;
        }

        return entity;
    }

    public void StageDelete<TEntity>(TEntity entity, bool softDelete = true) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (softDelete && entity is ISoftDeletableEntity softDeletable)
        {
            softDeletable.IsDeleted = true;
            StageUpdate(entity);
        }
        else
        {
            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _context.Set<TEntity>().Attach(entity);
            }
            entry.State = EntityState.Deleted;
        }
    }

    public void StageDeleteRange<TEntity>(IEnumerable<TEntity> entities, bool softDelete = true) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        foreach (var entity in entities)
        {
            StageDelete(entity, softDelete);
        }
    }

    public ValueTask<TEntity?> FindTrackedAsync<TEntity>(Guid id, CancellationToken ct = default) where TEntity : class
    {
        var local = _context.Set<TEntity>().Local.FirstOrDefault(e =>
        {
            if (e is BaseEntity be) return be.Id == id;
            var keyProp = _context.Entry(e).Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProp != null)
            {
                var val = _context.Entry(e).Property(keyProp.Name).CurrentValue;
                if (val is Guid g) return g == id;
            }
            return false;
        });

        if (local != null)
        {
            return ValueTask.FromResult<TEntity?>(local);
        }

        return _context.Set<TEntity>().FindAsync([id], ct);
    }

    public IReadOnlyCollection<object> GetAddedEntities() =>
        _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();

    public IReadOnlyCollection<object> GetModifiedEntities() =>
        _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified).Select(e => e.Entity).ToList();

    public IReadOnlyCollection<object> GetDeletedEntities() =>
        _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).Select(e => e.Entity).ToList();

    public IReadOnlyCollection<TEntity> GetStagedEntities<TEntity>() where TEntity : class =>
        _context.ChangeTracker.Entries<TEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity)
            .ToList();

    public void OnBeforeCommit(Func<ITransactionScope, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _beforeCommitHooks.Add(callback);
    }

    public void OnAfterCommit(Func<CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _afterCommitHooks.Add(callback);
    }

    public IReadOnlyCollection<string> AffectedEntityDescriptors => _affectedEntityDescriptors.ToList().AsReadOnly();

    /// <summary>
    /// Ingests domain events from change tracker BaseEntity instances into scope domain events.
    /// Also applies EntityDomainEventRegistry auto-emission for added or modified entities.
    /// </summary>
    public void IngestTrackedEntityDomainEvents()
    {
        foreach (var entry in _context.ChangeTracker.Entries<BaseEntity>())
        {
            var entityType = entry.Entity.GetType();
            var state = entry.State;

            if (state is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var identifier = entry.Entity.Id != Guid.Empty ? entry.Entity.Id.ToString() : "New";
                _affectedEntityDescriptors.Add($"{entityType.Name} [{state}]: {identifier}");
            }

            if (EntityDomainEventRegistry.ConfiguredEntities.TryGetValue(entityType, out var factory))
            {
                if (state == EntityState.Added && factory.CreateOnInsert != null)
                {
                    var eventKey = $"{entry.Entity.Id}:Created";
                    if (_synthesizedEventKeys.Add(eventKey))
                    {
                        var autoEvent = factory.CreateOnInsert(entry.Entity);
                        entry.Entity.RaiseDomainEvent(autoEvent);
                    }
                }
                else if (state == EntityState.Modified && factory.CreateOnUpdate != null)
                {
                    var eventKey = $"{entry.Entity.Id}:Updated";
                    if (_synthesizedEventKeys.Add(eventKey))
                    {
                        var autoEvent = factory.CreateOnUpdate(entry.Entity);
                        entry.Entity.RaiseDomainEvent(autoEvent);
                    }
                }
            }
        }

        var entityEvents = _context.ChangeTracker
            .Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        foreach (var ev in entityEvents)
        {
            _scopeDomainEvents.Add(ev);
        }
    }

    /// <summary>
    /// Harvests all domain events from tracked BaseEntity instances and scope-level registrations.
    /// Clears the domain events so they are only dispatched once.
    /// </summary>
    public List<IDomainEvent> HarvestDomainEvents()
    {
        IngestTrackedEntityDomainEvents();
        var allEvents = _scopeDomainEvents.ToList();
        _scopeDomainEvents.Clear();
        return allEvents;
    }
    /// <summary>
    /// Clears all domain events on tracked entities and the scope without dispatching them (used on rollback).
    /// </summary>
    public void ClearAllDomainEvents()
    {
        foreach (var entry in _context.ChangeTracker.Entries<BaseEntity>())
        {
            entry.Entity.ClearDomainEvents();
        }
        _scopeDomainEvents.Clear();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (IsRollbackOnly)
        {
            throw new InvalidOperationException("Transaction scope '" + OperationName + "' has been marked RollbackOnly and cannot be committed.");
        }

        // Ingest any entity-level domain events prior to intermediate save so they are not lost
        IngestTrackedEntityDomainEvents();

        _context.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _context.IsTransactionScopeCommitAuthorized = false;
        }
    }
    internal async Task<List<IDomainEvent>> ExecuteRootCommitAsync(CancellationToken cancellationToken)
    {
        if (IsRollbackOnly)
        {
            throw new InvalidOperationException($"Transaction scope '{OperationName}' has been marked RollbackOnly and cannot be committed.");
        }

        // 1. Execute pre-commit hooks
        foreach (var hook in _beforeCommitHooks)
        {
            await hook(this, cancellationToken);
        }

        // 2. Harvest all domain events before commit
        var harvestedDomainEvents = HarvestDomainEvents();

        // 3. Authorize SaveChanges and flush
        _context.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            if (_dbTransaction != null)
            {
                await _dbTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            _context.IsTransactionScopeCommitAuthorized = false;
        }

        // 4. Execute post-commit hooks
        foreach (var hook in _afterCommitHooks)
        {
            await hook(cancellationToken);
        }

        return harvestedDomainEvents;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_isRootScope)
        {
            AmbientTransactionContext.ClearCurrent();

            if (_dbTransaction != null)
            {
                await _dbTransaction.DisposeAsync();
            }
            await _context.DisposeAsync();
        }
    }
}
