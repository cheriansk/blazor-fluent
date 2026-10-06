using System.Data.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// Fail-closed singleton EF Core DbCommandInterceptor enforcing pure Zero-Trust architecture.
/// Denies ALL database command executions (reads, writes, scalars) if the execution context
/// is neither an authenticated user nor an authorized system daemon.
/// Resolves scoped user context dynamically from AppDbContext per command invocation.
/// Whitelists EF Core migrations, ASP.NET DataProtection key ring persistence, and forensic audit records.
/// </summary>
public class ZeroTrustDbCommandInterceptor : DbCommandInterceptor
{
    private readonly ILogger<ZeroTrustDbCommandInterceptor> _logger;

    public ZeroTrustDbCommandInterceptor(ILogger<ZeroTrustDbCommandInterceptor>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ZeroTrustDbCommandInterceptor>.Instance;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ValidateZeroTrustAccess(command, eventData);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ValidateZeroTrustAccess(command, eventData);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        ValidateZeroTrustAccess(command, eventData);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateZeroTrustAccess(command, eventData);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        ValidateZeroTrustAccess(command, eventData);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        ValidateZeroTrustAccess(command, eventData);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void ValidateZeroTrustAccess(DbCommand command, CommandEventData eventData)
    {
        var currentUser = (eventData.Context as AppDbContext)?.CurrentUser;

        // 1. Authenticated user or background system daemon allowed
        if (currentUser != null && (currentUser.IsAuthenticated || currentUser.IsSystemDaemon))
        {
            return;
        }

        // 2. Allow EF Core migrations engine (db.Database.Migrate and CLI dotnet ef commands)
        if (eventData.CommandSource == CommandSource.Migrations)
        {
            return;
        }

        // 3. Allow internal framework queries strictly needed during startup, key management, forensic audit recording, and active session tracking
        var cmdText = command.CommandText ?? string.Empty;
        if (cmdText.Contains("DataProtectionKeys", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("AuditRecords", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("UserSessions", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 4. Fail closed: Deny all unauthenticated database access
        _logger.LogCritical("ZERO-TRUST SECURITY VIOLATION: Denied unauthenticated database command execution. Command: {CommandText}", cmdText);
        throw new UnauthorizedAccessException("Zero-Trust Security Violation: Unauthenticated contexts are completely denied database query execution.");
    }
}
