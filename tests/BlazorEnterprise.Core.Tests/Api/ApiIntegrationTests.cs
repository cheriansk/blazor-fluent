using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BlazorEnterprise.Infrastructure.Security;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Api;

public class ApiTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestIssuer = "https://auth.company.com";
    public const string TestAudience = "https://api.company.com";
    public const string TestSigningKey = "BlazorEnterprise-Cloud-AuthGateway-MasterKey-2026-Secure!";
    public const string TrustedOrigin = "https://trusted.company.com";
    public const string ValidTenantId = "TENANT-001";
    public const string ValidLicenseKey = "DEV-COMMUNITY-KEY-0000-SECURE-KEY32";
    public const string TenantBId = "TENANT-002";
    public const string TenantBLicenseKey = "DEV-COMMUNITY-KEY-0002-SECURE-KEY32";

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
                [$"Tenants:{TenantBId}:LicenseKey"] = TenantBLicenseKey,
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
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("bm9uY2U=", "dGFn", "Y2lwaGVydGV4dA=="));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidTokenAndTenant_Returns200Ok()
    {
        var token = _factory.GenerateTestToken("user@company.com", ApiTestWebApplicationFactory.ValidTenantId, ["User"]);
        var encryptionKey = PayloadEncryptionUtility.DeriveTenantKey(ApiTestWebApplicationFactory.ValidLicenseKey, ApiTestWebApplicationFactory.ValidTenantId);

        var reqDto = new FileConvertReqDto(
            SessionId: Guid.NewGuid(),
            FileName: "test.csv",
            FileType: ImportFileTypeEnum.TaskCsv,
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
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("bm9uY2U=", "dGFn", "Y2lwaGVydGV4dA=="));

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
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("bm9uY2U=", "dGFn", "Y2lwaGVydGV4dA=="));

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

    [Fact]
    public async Task ProcessApi_WithWorkerS2SToken_SignedWithTenantLicenseKey_Returns200Ok()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var correlationContext = new CorrelationContext { CorrelationId = "CORR-WORKER-001" };
        var tokenService = new TenantTokenService(config, correlationContext);

        var s2sToken = tokenService.GenerateS2SToken(ApiTestWebApplicationFactory.ValidTenantId, "BatchWorker");
        var encryptionKey = PayloadEncryptionUtility.DeriveTenantKey(ApiTestWebApplicationFactory.ValidLicenseKey, ApiTestWebApplicationFactory.ValidTenantId);

        var reqDto = new FileConvertReqDto(
            SessionId: Guid.NewGuid(),
            FileName: "worker_batch.csv",
            FileType: ImportFileTypeEnum.TaskCsv,
            FileBytes: Encoding.UTF8.GetBytes("Title,Status\nWorker Task 1,Open\n")
        );

        var envelope = PayloadEncryptionUtility.EncryptPayload(reqDto, encryptionKey);

        using var request = CreateRequest(HttpMethod.Post, "/api/v1/transforms/convert", correlationId: "CORR-WORKER-001");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s2sToken);
        request.Content = JsonContent.Create(envelope, options: JsonDefaults.StandardOptions);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProcessApi_WithWorkerS2SToken_SignedWithMismatchedTenantLicenseKey_Returns401Unauthorized()
    {
        // Forged S2S token: Claims tid = ValidTenantId (Tenant A), but signed with Tenant B's license key
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyBytes = Encoding.UTF8.GetBytes(ApiTestWebApplicationFactory.TenantBLicenseKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "blazorenterprise-worker",
            Audience = "https://api.blazorenterprise.com",
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("tid", ApiTestWebApplicationFactory.ValidTenantId),
                new Claim("tenant_id", ApiTestWebApplicationFactory.ValidTenantId),
                new Claim("cid", "CORR-FORGED-001"),
                new Claim("act", "BatchWorker")
            }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var forgedToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        using var request = CreateRequest(HttpMethod.Post, "/api/v1/transforms/convert", correlationId: "CORR-FORGED-001");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", forgedToken);
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("bm9uY2U=", "dGFn", "Y2lwaGVydGV4dA=="));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProcessApi_WithWorkerS2SToken_Expired_Returns401Unauthorized()
    {
        // Expired S2S token (past 5-minute validity window)
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyBytes = Encoding.UTF8.GetBytes(ApiTestWebApplicationFactory.ValidLicenseKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "blazorenterprise-worker",
            Audience = "https://api.blazorenterprise.com",
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("tid", ApiTestWebApplicationFactory.ValidTenantId),
                new Claim("tenant_id", ApiTestWebApplicationFactory.ValidTenantId),
                new Claim("cid", "CORR-EXPIRED-001"),
                new Claim("act", "BatchWorker")
            }),
            NotBefore = DateTime.UtcNow.AddMinutes(-30),
            IssuedAt = DateTime.UtcNow.AddMinutes(-30),
            Expires = DateTime.UtcNow.AddMinutes(-10), // Expired 10 minutes ago
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var expiredToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        using var request = CreateRequest(HttpMethod.Post, "/api/v1/transforms/convert", correlationId: "CORR-EXPIRED-001");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        request.Content = JsonContent.Create(new EncryptedEnvelopeDto("bm9uY2U=", "dGFn", "Y2lwaGVydGV4dA=="));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
