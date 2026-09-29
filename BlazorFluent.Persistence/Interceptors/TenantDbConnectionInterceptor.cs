using System.Data.Common;
using BlazorFluent.Core.Contracts;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// EF Core connection interceptor enforcing PostgreSQL Native Row-Level Security (RLS) - Layer 5 defense-in-depth.
/// Whenever a database connection is opened from the connection pool, it executes set_config to initialize
/// 'app.is_host' and 'app.current_tenant_id' session variables so PostgreSQL natively rejects cross-tenant operations.
/// </summary>
public class TenantDbConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantDbConnectionInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        base.ConnectionOpened(connection, eventData);
        SetTenantSessionContext(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        await SetTenantSessionContextAsync(connection, cancellationToken);
    }

    private void SetTenantSessionContext(DbConnection connection)
    {
        // Skip execution for in-memory or SQLite providers in unit testing suites
        if (!IsPostgreSqlConnection(connection))
        {
            return;
        }

        using var command = connection.CreateCommand();
        ConfigureCommand(command);
        command.ExecuteNonQuery();
    }

    private async Task SetTenantSessionContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        // Skip execution for in-memory or SQLite providers in unit testing suites
        if (!IsPostgreSqlConnection(connection))
        {
            return;
        }

        using var command = connection.CreateCommand();
        ConfigureCommand(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void ConfigureCommand(DbCommand command)
    {
        var isHost = _tenantContext.IsHost ? "true" : "false";
        // Fail-closed: unauthenticated / empty tenant defaults to empty string so RLS matches 0 rows
        var tenantId = _tenantContext.IsHost ? string.Empty : (_tenantContext.TenantId ?? string.Empty);

        command.CommandText = "SELECT set_config('app.is_host', @is_host, false), set_config('app.current_tenant_id', @current_tenant_id, false);";

        var paramHost = command.CreateParameter();
        paramHost.ParameterName = "is_host";
        paramHost.Value = isHost;
        command.Parameters.Add(paramHost);

        var paramTenant = command.CreateParameter();
        paramTenant.ParameterName = "current_tenant_id";
        paramTenant.Value = tenantId;
        command.Parameters.Add(paramTenant);
    }

    private static bool IsPostgreSqlConnection(DbConnection connection)
    {
        return connection.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
    }
}
