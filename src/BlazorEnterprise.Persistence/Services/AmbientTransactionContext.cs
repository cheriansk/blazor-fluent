using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Ambient transaction coordinator using AsyncLocal to manage nesting and automatic joining of ITransactionScope.
/// </summary>
public static class AmbientTransactionContext
{
    private static readonly AsyncLocal<TransactionContext?> _currentScope = new();

    /// <summary>
    /// Gets the current active ambient TransactionContext, if any.
    /// </summary>
    public static TransactionContext? Current => _currentScope.Value;

    /// <summary>
    /// Acquires a write scope for state-mutating operations.
    /// If an ambient TransactionContext is already active, returns a nested scope bound to the shared context.
    /// Otherwise, creates a new root context with a dedicated database transaction.
    /// </summary>
    public static async ValueTask<ITransactionScope> AcquireWriteScopeAsync(
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken cancellationToken = default,
        string operationName = "WriteOperation")
    {
        var existing = _currentScope.Value;
        if (existing != null)
        {
            // Join existing ambient scope as a nested scope
            return new TransactionContext(existing.DbContext, null, isRootScope: false, operationName);
        }

        var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var root = new TransactionContext(db, tx, isRootScope: true, operationName);
        _currentScope.Value = root;
        return root;
    }

    internal static void SetCurrent(TransactionContext? context)
    {
        _currentScope.Value = context;
    }

    internal static void ClearCurrent()
    {
        _currentScope.Value = null;
    }
}
