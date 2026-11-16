using System.Diagnostics;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BlazorEnterprise.Web.Infrastructure.Observability;

/// <summary>
/// Centralized ASP.NET Core IExceptionHandler for BlazorEnterprise.Web.
/// Guarantees full-fidelity structured error logging with CorrelationId, zero sensitive error leakage,
/// and returns RFC 7807 ProblemDetails for API/fetch endpoints while delegating HTML pages to /Error.
/// </summary>
public sealed class WebExceptionHandler : IExceptionHandler
{
    private readonly ILogger<WebExceptionHandler> _logger;

    public WebExceptionHandler(ILogger<WebExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationContext = httpContext.RequestServices.GetService<ICorrelationContext>();
        var correlationId = !string.IsNullOrWhiteSpace(correlationContext?.CorrelationId)
            ? correlationContext.CorrelationId
            : Activity.Current?.Id ?? httpContext.TraceIdentifier;

        _logger.LogError(exception, "Unhandled Web exception occurred. CorrelationId: {CorrelationId}, Path: {Path}",
            correlationId, httpContext.Request.Path);

        // Check if the request is an API/JSON endpoint or explicitly accepts JSON
        var acceptHeader = httpContext.Request.Headers.Accept.ToString();
        var isApiOrJson = httpContext.Request.Path.StartsWithSegments("/api") ||
                          acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase);

        if (isApiOrJson)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred while processing your request.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Detail = $"Unable to complete operation. Please share Reference ID: '{correlationId}' with your administrator.",
                Instance = httpContext.Request.Path
            };

            problemDetails.Extensions["correlationId"] = correlationId;
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            httpContext.Response.StatusCode = problemDetails.Status.Value;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        // Return false to let the standard /Error page or status code re-execute handle browser rendering
        return false;
    }
}
