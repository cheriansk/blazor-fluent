using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Events;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Vendor-neutral transactional unit of work scope.
/// Enforces all staged inserts, updates, and deletes to commit together or roll back atomically.
/// Supports optimistic concurrency tracking via BaseEntity.Version (PostgreSQL xmin),
/// domain events, and pre/post-commit hooks.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>
    /// Unique identifier for this transaction scope instance.
    /// </summary>
    Guid ScopeId { get; }

    /// <summary>
    /// Logical name of the business operation executing in this scope.
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// Indicates whether changes have been staged in memory.
    /// </summary>
    bool HasStagedChanges { get; }

    /// <summary>
    /// Indicates whether this scope has been marked for rollback only.
    /// </summary>
    bool IsRollbackOnly { get; }

    /// <summary>
    /// Explicitly marks the transaction scope as rollback-only.
    /// Any subsequent attempt to commit will fail and abort.
    /// </summary>
    void MarkRollbackOnly();

    /// <summary>
    /// Stages an entity for insertion in the current transaction scope.
    /// </summary>
    TEntity StageInsert<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Stages a range of entities for insertion in the current transaction scope.
    /// </summary>
    void StageInsertRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;

    /// <summary>
    /// Stages an entity for update in the current transaction scope, validating and attaching
    /// expectedVersion (PostgreSQL xmin) for optimistic concurrency protection.
    /// </summary>
    TEntity StageUpdate<TEntity>(TEntity entity, uint? expectedVersion = null) where TEntity : class;

    /// <summary>
    /// Stages an entity for deletion (soft delete by default, or hard delete if specified/configured).
    /// </summary>
    void StageDelete<TEntity>(TEntity entity, bool softDelete = true) where TEntity : class;

    /// <summary>
    /// Stages a collection of entities for deletion.
    /// </summary>
    void StageDeleteRange<TEntity>(IEnumerable<TEntity> entities, bool softDelete = true) where TEntity : class;

    /// <summary>
    /// Locates an entity currently tracked in the active change tracker by primary key.
    /// </summary>
    ValueTask<TEntity?> FindTrackedAsync<TEntity>(Guid id, CancellationToken ct = default) where TEntity : class;

    /// <summary>
    /// Returns read-only snapshots of entities staged in memory.
    /// </summary>
    IReadOnlyCollection<object> GetAddedEntities();
    IReadOnlyCollection<object> GetModifiedEntities();
    IReadOnlyCollection<object> GetDeletedEntities();
    IReadOnlyCollection<TEntity> GetStagedEntities<TEntity>() where TEntity : class;

    /// <summary>
    /// Raises a domain event directly on the transaction scope.
    /// </summary>
    void RaiseDomainEvent(IDomainEvent domainEvent);

    /// <summary>
    /// Gets all domain events explicitly staged in this transaction scope.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> GetDomainEvents();

    /// <summary>
    /// Registers a callback to execute right before the database transaction commits.
    /// </summary>
    void OnBeforeCommit(Func<ITransactionScope, CancellationToken, Task> callback);

    /// <summary>
    /// Registers a callback to execute right after the database transaction has successfully committed.
    /// </summary>
    void OnAfterCommit(Func<CancellationToken, Task> callback);

    /// <summary>
    /// Flushes staged changes to the database. If this is a nested scope, it defers actual DB commit to the root scope.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
