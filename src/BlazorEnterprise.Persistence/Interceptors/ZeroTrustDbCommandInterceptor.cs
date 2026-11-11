using System;
using System.Data.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Interceptors;

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
        // 1. Allow EF Core migrations engine (db.Database.Migrate and CLI dotnet ef commands)
        if (eventData.CommandSource == CommandSource.Migrations)
        {
            return;
        }

        // 2. Allow internal framework queries strictly needed during startup, key management, forensic audit recording, active session tracking, and event tracking ledgers
        var cmdText = command.CommandText ?? string.Empty;
        if (cmdText.Contains("DataProtectionKeys", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("AuditRecords", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("UserSessions", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("EventPublishTrackers", StringComparison.OrdinalIgnoreCase) ||
            cmdText.Contains("EventConsumptionTrackers", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var appDbContext = eventData.Context as AppDbContext;
        var currentUser = appDbContext?.CurrentUser;

        // 2b. Gate 4: Mutation commands (INSERT, UPDATE, DELETE) must execute inside an active database transaction
        var trimmedCmd = cmdText.TrimStart();
        bool isMutation = trimmedCmd.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase) ||
                          trimmedCmd.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase) ||
                          trimmedCmd.StartsWith("DELETE ", StringComparison.OrdinalIgnoreCase);

        if (isMutation)
        {
            if (command.Transaction == null && appDbContext?.Database.CurrentTransaction == null)
            {
                _logger.LogCritical("TRANSACTION SECURITY VIOLATION: Denied database mutation command execution outside a transaction. Command: {CommandText}", cmdText);
                throw new InvalidOperationException("Transaction Security Violation: Database mutation commands (INSERT, UPDATE, DELETE) cannot be executed outside an active transaction scope.");
            }
        }

        // 3. Authenticated user or background system daemon allowed IF tenant context is valid
        if (currentUser != null && (currentUser.IsAuthenticated || currentUser.IsSystemDaemon))
        {
            var tenantContext = appDbContext?.TenantContext;
            if (tenantContext == null || !tenantContext.HasActiveTenant)
            {
                _logger.LogCritical("ZERO-TRUST SECURITY VIOLATION: Denied database command execution without active tenant context. Command: {CommandText}", command.CommandText);
                throw new System.Security.SecurityException("Zero-Trust Security Violation: Database command cannot be executed without an active tenant context.");
            }
            return;
        }

        // 4. Fail closed: Deny all unauthenticated database access
        _logger.LogCritical("ZERO-TRUST SECURITY VIOLATION: Denied unauthenticated database command execution. Command: {CommandText}", cmdText);
        throw new UnauthorizedAccessException("Zero-Trust Security Violation: Unauthenticated contexts are completely denied database query execution.");
    }
}
