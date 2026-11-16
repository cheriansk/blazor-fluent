using System.Diagnostics;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BlazorEnterprise.Api.Infrastructure;

/// <summary>
/// Centralized RFC 7807 ProblemDetails Global Exception Handler for BlazorEnterprise.Api.
/// Ensures zero sensitive error leakage to clients while capturing full fidelity in server logs with CorrelationId.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
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

        _logger.LogError(exception, "Unhandled API exception occurred. CorrelationId: {CorrelationId}, Path: {Path}",
            correlationId, httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
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
}
