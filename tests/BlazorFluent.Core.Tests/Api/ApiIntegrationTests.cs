using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Shared.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BlazorFluent.Core.Tests.Api;

public class ApiTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestIssuer = "https://auth.company.com";
    public const string TestAudience = "https://api.company.com";
    public const string TestSigningKey = "BlazorFluent-Cloud-AuthGateway-MasterKey-2026-Secure!";
    public const string TrustedOrigin = "https://trusted.company.com";
    public const string ValidTenantId = "TENANT-001";
    public const string ValidLicenseKey = "DEV-COMMUNITY-KEY-0000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = TestIssuer,
                ["Auth:Audience"] = TestAudience,
                ["Auth:SigningKey"] = TestSigningKey,
                ["AllowedOrigins:0"] = TrustedOrigin,
                [$"Tenants:{ValidTenantId}:LicenseKey"] = ValidLicenseKey,
                ["Admin:AllowedDomain"] = "company.com",
                ["Admin:KeyVaultUsers"] = "admin@company.com"
            });
        });
    }

    public string GenerateTestToken(string email, string? tenantId = null, string[]? roles = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, email),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(TestSigningKey));
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public class ApiIntegrationTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string? correlationId = "CORR-TEST-001")
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.Add(EngineHeaderConstants.CorrelationId, correlationId);
        }
        return request;
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401Unauthorized()
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/transforms/convert");
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("test", "test", "test", "test", "test"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidTokenAndTenant_Returns200Ok()
    {
        var token = _factory.GenerateTestToken("user@company.com", ApiTestWebApplicationFactory.ValidTenantId, ["User"]);
        var encryptionKey = PayloadEncryptionUtility.DeriveTenantKey(ApiTestWebApplicationFactory.ValidLicenseKey, ApiTestWebApplicationFactory.ValidTenantId);

        var reqDto = new FileConvertReqDto(
            SessionId: "session-1",
            FileName: "test.csv",
            FileType: ImportFileType.TaskCsv,
            FileBytes: Encoding.UTF8.GetBytes("Title,Status\nTask 1,Open\n")
        );

        var envelope = PayloadEncryptionUtility.EncryptPayload(reqDto, encryptionKey);

        using var request = CreateRequest(HttpMethod.Post, "/api/v1/transforms/convert");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(envelope, options: JsonDefaults.StandardOptions);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutTenantClaim_Returns401Unauthorized()
    {
        // Valid signature, but missing tenant_id claim
        var token = _factory.GenerateTestToken("user@company.com", tenantId: null, ["User"]);

        using var request = CreateRequest(HttpMethod.Post, "/api/v1/transforms/convert");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("a", "b", "c", "d", "e"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithUnconfiguredTenant_Returns403Forbidden()
    {
        // Token has a tenant, but configuration has NO license key for this tenant (prohibiting shared key fallback)
        var token = _factory.GenerateTestToken("user@unconfigured.com", "UNCONFIGURED-TENANT-999", ["User"]);

        using var request = CreateRequest(HttpMethod.Post, "/api/v1/transforms/convert");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("a", "b", "c", "d", "e"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthEndpoint_Authorize_AllowsAnonymousAccess()
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/auth/authorize");
        request.Content = JsonContent.Create(new { email = "admin@company.com", clientId = "client-app", redirectUri = "https://app" });

        var response = await _client.SendAsync(request);

        // Does NOT return 401 Unauthorized because endpoint is AllowAnonymous
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithoutAdminRole_Returns403Forbidden()
    {
        var token = _factory.GenerateTestToken("user@company.com", ApiTestWebApplicationFactory.ValidTenantId, ["User"]);

        using var request = CreateRequest(HttpMethod.Get, "/api/v1/admin/entitlements");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoint_WithAdminRole_Returns200Ok()
    {
        var token = _factory.GenerateTestToken("admin@company.com", ApiTestWebApplicationFactory.ValidTenantId, ["Admin"]);

        using var request = CreateRequest(HttpMethod.Get, "/api/v1/admin/entitlements");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cors_AllowedOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        using var request = CreateRequest(HttpMethod.Get, "/");
        request.Headers.Add("Origin", ApiTestWebApplicationFactory.TrustedOrigin);

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain(ApiTestWebApplicationFactory.TrustedOrigin);
    }

    [Fact]
    public async Task Cors_DisallowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        using var request = CreateRequest(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "https://untrusted-external-site.com");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Request_WithoutCorrelationId_Returns400BadRequest()
    {
        // Missing mandatory X-Correlation-Id header
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/admin/entitlements", correlationId: null);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
