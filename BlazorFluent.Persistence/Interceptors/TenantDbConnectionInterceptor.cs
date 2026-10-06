using System.Data.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// Singleton EF Core connection interceptor enforcing PostgreSQL Native Row-Level Security (RLS) - Layer 5 defense-in-depth.
/// Dynamically extracts the active ITenantContext from AppDbContext per connection open.
/// Whenever a database connection is opened from the connection pool, it executes set_config to initialize
/// 'app.is_host' and 'app.current_tenant_id' session variables so PostgreSQL natively rejects cross-tenant operations.
/// </summary>
public class TenantDbConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext? _fallbackTenantContext;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TenantDbConnectionInterceptor()
    {
        _fallbackTenantContext = null;
    }

    public TenantDbConnectionInterceptor(ITenantContext fallbackTenantContext)
    {
        _fallbackTenantContext = fallbackTenantContext;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        base.ConnectionOpened(connection, eventData);
        SetTenantSessionContext(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        await SetTenantSessionContextAsync(connection, eventData, cancellationToken);
    }

    private void SetTenantSessionContext(DbConnection connection, ConnectionEndEventData eventData)
    {
        // Skip execution for in-memory or SQLite providers in unit testing suites
        if (!IsPostgreSqlConnection(connection))
        {
            return;
        }

        var tenantContext = (eventData.Context as AppDbContext)?.TenantContext ?? _fallbackTenantContext;
        using var command = connection.CreateCommand();
        ConfigureCommand(command, tenantContext);
        command.ExecuteNonQuery();
    }

    private async Task SetTenantSessionContextAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken)
    {
        // Skip execution for in-memory or SQLite providers in unit testing suites
        if (!IsPostgreSqlConnection(connection))
        {
            return;
        }

        var tenantContext = (eventData.Context as AppDbContext)?.TenantContext ?? _fallbackTenantContext;
        using var command = connection.CreateCommand();
        ConfigureCommand(command, tenantContext);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ConfigureCommand(DbCommand command, ITenantContext? tenantContext)
    {
        var isHost = tenantContext?.IsHost == true ? "true" : "false";
        // Fail-closed: unauthenticated / empty tenant defaults to empty string so RLS matches 0 rows
        var tenantId = tenantContext?.IsHost == true ? string.Empty : (tenantContext?.TenantId ?? string.Empty);

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
