using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorEnterprise.Shared.Observability;

/// <summary>
/// Distributed tracing and observability primitives shared across Client WebApp and Cloud ProcessAPI.
/// </summary>
public static class SharedObservabilityExtensions
{
    public const string ServiceNamespace = "BlazorEnterprise";
    public static readonly ActivitySource AppActivitySource = new(ServiceNamespace, "1.0.0");

    /// <summary>
    /// Starts an activity tagged with the mandatory correlation ID and tenant context.
    /// </summary>
    public static Activity? StartCorrelatedActivity(string operationName, string correlationId, string? tenantId = null)
    {
        var activity = AppActivitySource.StartActivity(operationName);
        if (activity != null)
        {
            activity.SetTag("blazorenterprise.correlation_id", correlationId);
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                activity.SetTag("blazorenterprise.tenant_id", tenantId);
            }
        }
        return activity;
    }
}
