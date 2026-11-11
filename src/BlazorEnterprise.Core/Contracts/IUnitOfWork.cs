using BlazorEnterprise.Core.Dtos.Response;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Service contract providing resilient, atomic PostgreSQL database transactions.
/// Guarantees all-or-nothing execution for multi-step database workflows with automated rollback,
/// change-tracker detachment, isolated forensic exception auditing, and Milan Jovanović's Domain Events lifecycle.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Gets the current ambient transaction scope if active.
    /// </summary>
    ITransactionScope? CurrentScope { get; }

    /// <summary>
    /// Flushes changes and commits the current transaction scope, returning the number of affected rows.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a delegate within an explicit PostgreSQL transaction wrapped in EF Core ExecutionStrategy.
    /// If an exception occurs or the action returns a failed Result:
    /// - Rolls back the transaction immediately.
    /// - Clears the DbContext change tracker to prevent poisoned entity state in memory.
    /// - Records the exception in audit.AuditRecords and Serilog via an isolated scope.
    /// - Dispatches UnitOfWorkFailedDomainEvent.
    /// - Returns a sanitized Result.Failure containing the correlation Incident Reference ID.
    /// </summary>
    Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> action,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-generic variant executing a database workflow in an atomic transaction.
    /// </summary>
    Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> action,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a delegate receiving the ambient ITransactionScope within an explicit PostgreSQL transaction.
    /// Supports staging entities directly into the scope with full concurrency verification and domain event dispatching.
    /// </summary>
    Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<ITransactionScope, CancellationToken, Task<Result<T>>> action,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-generic variant executing a delegate receiving the ambient ITransactionScope in an atomic transaction.
    /// </summary>
    Task<Result> ExecuteInTransactionAsync(
        Func<ITransactionScope, CancellationToken, Task<Result>> action,
        string operationName,
        CancellationToken cancellationToken = default);
}
