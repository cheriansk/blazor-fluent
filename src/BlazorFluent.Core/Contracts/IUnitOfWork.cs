using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service contract providing resilient, atomic PostgreSQL database transactions.
/// Guarantees all-or-nothing execution for multi-step database workflows with automated rollback,
/// change-tracker detachment, and isolated forensic exception auditing.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Executes a delegate within an explicit PostgreSQL transaction wrapped in EF Core ExecutionStrategy.
    /// If an exception occurs or the action returns a failed Result:
    /// - Rolls back the transaction immediately.
    /// - Clears the DbContext change tracker to prevent poisoned entity state in memory.
    /// - Records the exception in audit.AuditRecords and Serilog via an isolated scope.
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
}
