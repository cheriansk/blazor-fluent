using System.Net.Mime;
using System.Text.Json;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Client;

namespace BlazorFluent.Api.Middleware;

/// <summary>
/// Strict security middleware enforcing that every single incoming request contains a valid X-Correlation-Id header.
/// The Cloud API NEVER generates its own correlation identifier. Missing or malformed headers are rejected with 400 Bad Request.
/// </summary>
public class EnforceMandatoryCorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnforceMandatoryCorrelationIdMiddleware> _logger;

    public EnforceMandatoryCorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<EnforceMandatoryCorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Exempt CORS preflight, health checks, static assets, and favicon
        if (HttpMethods.IsOptions(context.Request.Method) ||
            path.StartsWith("/health") || path.StartsWith("/alive") ||
            path.EndsWith(".ico") || path.EndsWith(".css") || path.EndsWith(".js"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(EngineHeaderConstants.CorrelationId, out var correlationHeader) ||
            string.IsNullOrWhiteSpace(correlationHeader))
        {
            _logger.LogWarning("Rejected inbound request to {Path} - Missing mandatory header: {Header}",
                context.Request.Path, EngineHeaderConstants.CorrelationId);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var errorPayload = new
            {
                error = "MandatoryCorrelationIdMissing",
                message = $"Missing mandatory header '{EngineHeaderConstants.CorrelationId}'. Cloud ProcessAPI strictly enforces caller-supplied correlation IDs and never generates its own.",
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorPayload, JsonDefaults.StandardOptions));
            return;
        }

        var rawId = correlationHeader.ToString();
        var sanitizedId = CorrelationIdSanitizer.Sanitize(rawId);

        if (sanitizedId == null)
        {
            _logger.LogWarning("Rejected inbound request to {Path} - Invalid correlation header format: {RawId}",
                context.Request.Path, rawId);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var errorPayload = new
            {
                error = "InvalidCorrelationIdFormat",
                message = $"The provided '{EngineHeaderConstants.CorrelationId}' contains invalid characters or exceeds 64 characters.",
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorPayload, JsonDefaults.StandardOptions));
            return;
        }

        // Store in ambient context and response header
        correlationContext.CorrelationId = sanitizedId;
        context.Response.Headers[EngineHeaderConstants.CorrelationId] = sanitizedId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = sanitizedId }))
        {
            await _next(context);
        }
    }
}
