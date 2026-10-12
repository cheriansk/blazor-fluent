using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Unit of work implementation wrapping multi-step database workflows in an explicit PostgreSQL transaction.
/// Enforces all-or-nothing consistency: if an error occurs, automatically rolls back the transaction,
/// detaches modified entities from the change tracker, logs the incident, and returns a sanitized Result.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(AppDbContext dbContext, IAuditService auditService, ILogger<UnitOfWork> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> action,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Transaction [{Operation}] failed with business error. Rolling back changes.", operationName);
                    await transaction.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                    return result;
                }

                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction [{Operation}] encountered an unhandled exception. Rolling back.", operationName);
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();

                var errorId = await _auditService.LogExceptionAsync(
                    ex,
                    $"Database Transaction [{operationName}]",
                    AuditSeverity.Error,
                    cancellationToken);

                return Result<T>.Failure($"An unexpected error occurred during '{operationName}'. Reference: {errorId}");
            }
        });
    }

    public async Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> action,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteInTransactionAsync<object?>(async ct =>
        {
            var res = await action(ct);
            return res.Succeeded
                ? Result<object?>.Success(null, res.Message)
                : Result<object?>.Failure(res.Errors);
        }, operationName, cancellationToken);

        return result.Succeeded ? Result.Success(result.Message) : Result.Failure(result.Errors);
    }
}
