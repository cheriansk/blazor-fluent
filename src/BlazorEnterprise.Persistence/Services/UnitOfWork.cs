using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Events;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Unit of work implementation wrapping multi-step database workflows in an explicit PostgreSQL transaction.
/// Enforces all-or-nothing consistency: if an error occurs, automatically rolls back the transaction,
/// detaches modified entities from the change tracker, logs the incident, and returns a sanitized Result.
/// Supports Milan Jovanović's Domain Events pattern: harvests entity domain events and dispatches
/// lifecycle domain events on both success and failure.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService auditService,
        IDomainEventDispatcher domainEventDispatcher,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ILogger<UnitOfWork> logger)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _domainEventDispatcher = domainEventDispatcher;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public ITransactionScope? CurrentScope => AmbientTransactionContext.Current;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (AmbientTransactionContext.Current is { } scope)
        {
            await scope.SaveChangesAsync(cancellationToken);
            return 1;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.IsTransactionScopeCommitAuthorized = true;
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            db.IsTransactionScopeCommitAuthorized = false;
        }
    }

    public Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> action,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync<T>(
            (scope, ct) => action(ct),
            operationName,
            cancellationToken);
    }

    public Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> action,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync(
            (scope, ct) => action(ct),
            operationName,
            cancellationToken);
    }

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<ITransactionScope, CancellationToken, Task<Result<T>>> action,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        // If an ambient scope is already active, enlist in it as a nested scope
        if (AmbientTransactionContext.Current != null)
        {
            await using var nestedScope = await AmbientTransactionContext.AcquireWriteScopeAsync(_dbFactory, cancellationToken, operationName);
            var nestedResult = await action(nestedScope, cancellationToken);
            if (!nestedResult.Succeeded)
            {
                nestedScope.MarkRollbackOnly();
            }
            return nestedResult;
        }

        var ambientFactory = _dbFactory as AmbientAwareDbContextFactory;
        var db = ambientFactory != null 
            ? await ambientFactory.CreateIndependentDbContextAsync(cancellationToken) 
            : await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.IsManagedByAmbientScope = true;
        var strategy = db.Database.CreateExecutionStrategy();

        var tenantId = _tenantContext.HasActiveTenant ? _tenantContext.TenantId : Guid.Empty;
        var userId = _currentUser.UserId;
        var userEmail = _currentUser.Email;

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var rootScope = new TransactionContext(db, transaction, isRootScope: true, operationName);
            AmbientTransactionContext.SetCurrent(rootScope);

            try
            {
                var result = await action(rootScope, cancellationToken);
                if (!result.Succeeded || rootScope.IsRollbackOnly)
                {
                    _logger.LogWarning("Transaction [{Operation}] failed with business error. Rolling back changes.", operationName);
                    var attemptedEntities = rootScope.AffectedEntityDescriptors.ToList();
                    rootScope.ClearAllDomainEvents();
                    await transaction.RollbackAsync(cancellationToken);
                    db.ChangeTracker.Clear();

                    // Dispatch UnitOfWorkFailedDomainEvent after rollback
                    var failedErrors = result.Errors?.ToList() ?? (!string.IsNullOrWhiteSpace(result.Message) ? [result.Message] : new List<string> { "Transaction aborted" });
                    await SafeDispatchAsync(new UnitOfWorkFailedDomainEvent(
                        ScopeId: rootScope.ScopeId,
                        OperationName: operationName,
                        TenantId: tenantId,
                        UserId: userId,
                        UserEmail: userEmail,
                        AttemptedEntities: attemptedEntities,
                        Errors: failedErrors,
                        ExceptionType: null,
                        DiagnosticDetails: null,
                        IncidentReferenceId: null,
                        IsConcurrencyConflict: false)
                    {
                        CorrelationId = rootScope.ScopeId.ToString("N")[..12]
                    }, cancellationToken);

                    return result;
                }

                // Execute root commit (runs OnBeforeCommit, harvests domain events, SaveChangesAsync, CommitAsync, OnAfterCommit)
                var harvestedEvents = await rootScope.ExecuteRootCommitAsync(cancellationToken);
                var affectedEntities = rootScope.AffectedEntityDescriptors.ToList();

                // Dispatch harvested entity domain events + UnitOfWorkSucceededDomainEvent
                foreach (var domainEvent in harvestedEvents)
                {
                    if (string.IsNullOrWhiteSpace(domainEvent.CorrelationId))
                    {
                        // DomainEventBase init property
                    }
                    await SafeDispatchAsync(domainEvent, cancellationToken);
                }

                await SafeDispatchAsync(new UnitOfWorkSucceededDomainEvent(
                    ScopeId: rootScope.ScopeId,
                    OperationName: operationName,
                    TenantId: tenantId,
                    UserId: userId,
                    UserEmail: userEmail,
                    AffectedEntities: affectedEntities,
                    EntityDomainEventCount: harvestedEvents.Count)
                {
                    CorrelationId = rootScope.ScopeId.ToString("N")[..12]
                }, cancellationToken);

                return result;
            }
            catch (DbUpdateConcurrencyException cEx)
            {
                _logger.LogWarning(cEx, "Concurrency conflict during transaction [{Operation}]. Rolling back.", operationName);
                var attemptedEntities = rootScope.AffectedEntityDescriptors.ToList();
                rootScope.ClearAllDomainEvents();
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();

                await SafeDispatchAsync(new UnitOfWorkFailedDomainEvent(
                    ScopeId: rootScope.ScopeId,
                    OperationName: operationName,
                    TenantId: tenantId,
                    UserId: userId,
                    UserEmail: userEmail,
                    AttemptedEntities: attemptedEntities,
                    Errors: ["Concurrency Conflict: The record was modified by another user or batch operation."],
                    ExceptionType: nameof(DbUpdateConcurrencyException),
                    DiagnosticDetails: cEx.Message,
                    IncidentReferenceId: null,
                    IsConcurrencyConflict: true)
                {
                    CorrelationId = rootScope.ScopeId.ToString("N")[..12]
                }, cancellationToken);

                return Result<T>.Failure("Concurrency Conflict: The record was modified by another user or batch operation. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction [{Operation}] encountered an unhandled exception. Rolling back.", operationName);
                var attemptedEntities = rootScope.AffectedEntityDescriptors.ToList();
                rootScope.ClearAllDomainEvents();
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();

                var errorId = await _auditService.LogExceptionAsync(
                    ex,
                    $"Database Transaction [{operationName}]",
                    AuditSeverityEnum.Error,
                    cancellationToken);

                var diag = ex.InnerException != null ? $"{ex.Message} ---> {ex.InnerException.Message}" : ex.Message;

                await SafeDispatchAsync(new UnitOfWorkFailedDomainEvent(
                    ScopeId: rootScope.ScopeId,
                    OperationName: operationName,
                    TenantId: tenantId,
                    UserId: userId,
                    UserEmail: userEmail,
                    AttemptedEntities: attemptedEntities,
                    Errors: [ex.Message],
                    ExceptionType: ex.GetType().Name,
                    DiagnosticDetails: diag,
                    IncidentReferenceId: errorId,
                    IsConcurrencyConflict: false)
                {
                    CorrelationId = rootScope.ScopeId.ToString("N")[..12]
                }, cancellationToken);

                return Result<T>.Failure($"An unexpected error occurred during '{operationName}'. Reference: {errorId}");
            }
            finally
            {
                db.IsManagedByAmbientScope = false;
                AmbientTransactionContext.ClearCurrent();
                await db.DisposeAsync();
            }
        });
    }

    public async Task<Result> ExecuteInTransactionAsync(
        Func<ITransactionScope, CancellationToken, Task<Result>> action,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteInTransactionAsync<object?>(async (scope, ct) =>
        {
            var res = await action(scope, ct);
            return res.Succeeded
                ? Result<object?>.Success(null, res.Message)
                : Result<object?>.Failure(res.Errors);
        }, operationName, cancellationToken);

        return result.Succeeded ? Result.Success(result.Message) : Result.Failure(result.Errors);
    }

    private async Task SafeDispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during domain event dispatch: {EventName}", domainEvent.GetType().Name);
        }
    }
}
