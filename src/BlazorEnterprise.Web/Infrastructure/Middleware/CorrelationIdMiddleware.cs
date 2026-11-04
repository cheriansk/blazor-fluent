using System.Diagnostics;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace BlazorEnterprise.Web.Infrastructure.Middleware;

/// <summary>
/// Ensures every incoming HTTP request and Blazor SignalR connection has a consistent Correlation ID.
/// Propagates the identifier to response headers, ICorrelationContext, Activity tags, and Serilog LogContext.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationHeader = EngineHeaderConstants.CorrelationId; // "X-Correlation-Id"
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        string? correlationId = null;

        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var headerValues) &&
            !string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
        {
            correlationId = CorrelationIdSanitizer.Sanitize(headerValues.FirstOrDefault());
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..12];
        }

        correlationContext.CorrelationId = correlationId;

        if (!context.Response.Headers.ContainsKey(CorrelationHeader))
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
        }

        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("blazorenterprise.correlation_id", correlationId);
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
