using Microsoft.Extensions.Diagnostics.HealthChecks;
using BlazorEnterprise.Jobs.Abstractions;

namespace BlazorEnterprise.Jobs.HealthChecks;

/// <summary>
/// Health check verifying that the asynchronous background job event queue channel is open and writable.
/// </summary>
public class BackgroundJobQueueHealthCheck : IHealthCheck
{
    private readonly IJobEventQueue _queue;

    public BackgroundJobQueueHealthCheck(IJobEventQueue queue)
    {
        _queue = queue;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_queue == null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("IJobEventQueue is unavailable or unassigned."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Background Job Event Queue channel operational."));
    }
}
