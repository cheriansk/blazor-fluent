using System.Net.Http.Headers;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Infrastructure.Security;
using BlazorFluent.Shared.Contracts.Engine.Client;

namespace BlazorFluent.Infrastructure.EngineGateway;

/// <summary>
/// Delegating handler for outbound HttpClient requests to Cloud ProcessAPI.
/// Automatically resolves active ITenantContext, mints an ephemeral S2S JWT signed with the tenant's license key,
/// and attaches standard Authorization: Bearer, X-Tenant-Id, and X-Correlation-Id headers.
/// </summary>
public class TenantAuthDelegatingHandler : DelegatingHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantTokenService _tokenService;
    private readonly ICorrelationContext _correlationContext;

    public TenantAuthDelegatingHandler(
        ITenantContext tenantContext,
        ITenantTokenService tokenService,
        ICorrelationContext correlationContext)
    {
        _tenantContext = tenantContext;
        _tokenService = tokenService;
        _correlationContext = correlationContext;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Cannot invoke Cloud ProcessAPI without an established TenantId context.");
        }

        // Mint ephemeral S2S token signed with this tenant's license key
        var token = _tokenService.GenerateS2SToken(tenantId);

        // Attach Authorization header if not already set
        request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer", token);

        // Attach tenant identity header for upstream routing
        if (!request.Headers.Contains("X-Tenant-Id"))
        {
            request.Headers.Add("X-Tenant-Id", tenantId);
        }

        // Forward distributed CorrelationId
        if (!string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) && !request.Headers.Contains("X-Correlation-Id"))
        {
            request.Headers.Add("X-Correlation-Id", _correlationContext.CorrelationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
