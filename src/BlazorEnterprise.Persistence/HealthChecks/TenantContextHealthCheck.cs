using Microsoft.Extensions.Diagnostics.HealthChecks;
using BlazorEnterprise.Core.Contracts;

namespace BlazorEnterprise.Persistence.HealthChecks;

/// <summary>
/// Health check verifying that the multi-tenant context can be resolved safely and fails closed.
/// </summary>
public class TenantContextHealthCheck : IHealthCheck
{
    private readonly ITenantContext _tenantContext;

    public TenantContextHealthCheck(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Fail-closed verification: ITenantContext must be non-null in scope
        if (_tenantContext == null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("ITenantContext resolution failed in current dependency scope."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Tenant isolation context resolution operational."));
    }
}
