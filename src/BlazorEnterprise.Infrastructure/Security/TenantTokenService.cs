using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BlazorEnterprise.Infrastructure.Security;

/// <summary>
/// Production implementation of <see cref="ITenantTokenService"/> minting ephemeral S2S tokens.
/// Uses Per-Tenant Secret Signing (RFC 7519 / RFC 7523):
/// 1. Signed exclusively using the tenant's private TenantLicenseKey.
/// 2. Carries opaque identifiers (tid, cid, act) with zero PII and zero database PKs.
/// 3. Ephemeral 5-minute lifetime preventing token replay.
/// </summary>
public class TenantTokenService : ITenantTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ICorrelationContext _correlationContext;

    public TenantTokenService(IConfiguration configuration, ICorrelationContext correlationContext)
    {
        _configuration = configuration;
        _correlationContext = correlationContext;
    }

    public string GenerateS2SToken(Guid tenantId, string actorType = "BatchWorker")
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Cannot mint an S2S token without an established TenantId.");
        }

        return GenerateS2SToken(tenantId.ToString(), actorType);
    }

    public string GenerateS2SToken(string tenantId, string actorType = "BatchWorker")
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Cannot mint an S2S token without an established TenantId.");
        }

        var licenseKey = _configuration[$"Tenants:{tenantId}:LicenseKey"];
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new InvalidOperationException($"Zero-Trust Security Violation: No license key configured for tenant '{tenantId}'. Shared fallback is strictly prohibited.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(licenseKey);
        var tokenHandler = new JwtSecurityTokenHandler();
        var correlationId = !string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? _correlationContext.CorrelationId
            : Guid.CreateVersion7().ToString("N")[..12];

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "blazorenterprise-worker",
            Audience = "https://api.blazorenterprise.com",
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("tid", tenantId),
                new Claim("tenant_id", tenantId),
                new Claim("cid", correlationId),
                new Claim("act", actorType)
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
