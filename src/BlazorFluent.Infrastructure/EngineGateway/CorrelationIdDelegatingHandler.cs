using System.Net.Http.Headers;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.EngineGateway;

/// <summary>
/// Outbound delegating handler that guarantees mandatory X-Correlation-Id and X-Tenant-License-Key
/// are attached to every single request destined for Company Azure ProcessAPI.
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly ICorrelationContext _correlationContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CorrelationIdDelegatingHandler> _logger;

    public CorrelationIdDelegatingHandler(
        ICorrelationContext correlationContext,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<CorrelationIdDelegatingHandler> logger)
    {
        _correlationContext = correlationContext;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. Resolve Correlation ID (Caller must provide or active ambient context must hold it)
        var correlationId = _correlationContext.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId) && _httpContextAccessor.HttpContext != null)
        {
            if (_httpContextAccessor.HttpContext.Request.Headers.TryGetValue(EngineHeaderConstants.CorrelationId, out var headerVal))
            {
                correlationId = headerVal.ToString();
            }
            else if (_httpContextAccessor.HttpContext.Items.TryGetValue("CorrelationId", out var itemVal))
            {
                correlationId = itemVal?.ToString() ?? string.Empty;
            }
        }

        // If not set anywhere in caller context, generate an activity-based or GUID ID as fallback
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
            _correlationContext.CorrelationId = correlationId;
            _logger.LogWarning("Outbound engine request originated without explicit CorrelationId; synthesized: {CorrelationId}", correlationId);
        }

        // Defensive sanitization against CRLF injection
        var sanitizedCorrelationId = CorrelationIdSanitizer.Sanitize(correlationId) ?? Guid.NewGuid().ToString("N");

        // Attach mandatory header
        request.Headers.Remove(EngineHeaderConstants.CorrelationId);
        request.Headers.Add(EngineHeaderConstants.CorrelationId, sanitizedCorrelationId);

        // 2. Attach Tenant License Key
        var licenseKey = _configuration["Engine:LicenseKey"] ?? _configuration["Tenant:LicenseKey"];
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            request.Headers.Remove(EngineHeaderConstants.LicenseKey);
            request.Headers.Add(EngineHeaderConstants.LicenseKey, licenseKey.Trim());
        }

        // 3. Attach Client Assembly Version
        request.Headers.Remove(EngineHeaderConstants.ClientVersion);
        request.Headers.Add(EngineHeaderConstants.ClientVersion, "1.0.0");

        return await base.SendAsync(request, cancellationToken);
    }
}
