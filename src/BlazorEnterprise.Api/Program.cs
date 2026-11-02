using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using BlazorEnterprise.Api.Admin;
using BlazorEnterprise.Api.Auth;
using BlazorEnterprise.Api.Endpoints;
using BlazorEnterprise.Api.Middleware;
using BlazorEnterprise.RulesEngine.BatchProcessing;
using BlazorEnterprise.RulesEngine.BatchProcessing.Processors;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using BlazorEnterprise.Shared.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Shared Serilog setup
builder.Host.UseSharedSerilog("BlazorEnterprise.Api");

// 2. Add endpoints exploration
builder.Services.AddEndpointsApiExplorer();

// 3. Register Correlation Context (ambient scoped context)
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();

// 4. Register Multi-IdP Auth Gateway and Entitlements
builder.Services.AddSingleton<IEntitlementsRepository, InMemoryEntitlementsRepository>();
builder.Services.AddScoped<IAuthGatewayService, AuthGatewayService>();
builder.Services.AddScoped<IAdminSecurityService, AdminSecurityService>();

// 4b. Register .NET 10 HybridCache (L1 RAM + optional L2 Redis) & Stateless Cloud Batch Processors
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConn;
        options.InstanceName = "BlazorEnterpriseApi:";
    });
}

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 10 * 1024 * 1024; // 10 MB per cached batch entry
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

builder.Services.AddSingleton<BaseCloudProcessor, TaskCloudProcessor>();
builder.Services.AddSingleton<CloudProcessorDispatcher>();

// Strongly-typed options registration
builder.Services.AddOptions<BlazorEnterprise.Core.Configuration.AuthOptions>()
    .BindConfiguration(BlazorEnterprise.Core.Configuration.AuthOptions.SectionName);
builder.Services.AddOptions<BlazorEnterprise.Core.Configuration.AdminOptions>()
    .BindConfiguration(BlazorEnterprise.Core.Configuration.AdminOptions.SectionName);
builder.Services.AddOptions<BlazorEnterprise.Core.Configuration.EngineOptions>()
    .BindConfiguration(BlazorEnterprise.Core.Configuration.EngineOptions.SectionName)
    .PostConfigure<IConfiguration>((options, configuration) =>
    {
        if (string.IsNullOrWhiteSpace(options.LicenseKey) && !string.IsNullOrWhiteSpace(configuration["Tenant:LicenseKey"]))
        {
            options.LicenseKey = configuration["Tenant:LicenseKey"]!;
        }
        foreach (var tenant in configuration.GetSection("Tenants").GetChildren())
        {
            var lk = tenant["LicenseKey"];
            if (!string.IsNullOrWhiteSpace(lk))
            {
                options.TenantLicenseKeys[tenant.Key] = lk;
            }
        }
    });
builder.Services.AddOptions<BlazorEnterprise.Core.Configuration.ProprietaryStorageOptions>()
    .BindConfiguration(BlazorEnterprise.Core.Configuration.ProprietaryStorageOptions.SectionName);

// 5. JWT Bearer Authentication (Zero-Trust Fail-Fast Configuration Validation)
var authIssuer = builder.Configuration["Auth:Issuer"];
if (string.IsNullOrWhiteSpace(authIssuer))
{
    throw new InvalidOperationException("Zero-Trust Security Violation: 'Auth:Issuer' must be explicitly configured.");
}

var authAudience = builder.Configuration["Auth:Audience"];
if (string.IsNullOrWhiteSpace(authAudience))
{
    throw new InvalidOperationException("Zero-Trust Security Violation: 'Auth:Audience' must be explicitly configured.");
}

var authSigningKey = builder.Configuration["Auth:SigningKey"];
if (string.IsNullOrWhiteSpace(authSigningKey) || authSigningKey.Length < 32)
{
    throw new InvalidOperationException("Zero-Trust Security Violation: 'Auth:SigningKey' must be explicitly configured with at least 32 characters.");
}

var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(authSigningKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            {
                var keys = new List<SecurityKey>();
                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwt = handler.ReadJwtToken(token);
                        var tenantId = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_id" || c.Type == "tid")?.Value;
                        if (!string.IsNullOrWhiteSpace(tenantId))
                        {
                            var tenantKey = builder.Configuration[$"Tenants:{tenantId}:LicenseKey"];
                            if (!string.IsNullOrWhiteSpace(tenantKey))
                            {
                                keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tenantKey)));
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parsing error and fallback to master key
                }

                // Also allow the central Cloud Master Signing Key for user login tokens
                var currentSigningKey = builder.Configuration["Auth:SigningKey"] ?? authSigningKey;
                keys.Add(new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(currentSigningKey))));
                return keys;
            },
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// 6. Fail-Closed Fallback Authorization & Admin Policy
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("tenant_id"));
});

// 7. Restrictive CORS Policy (Zero Fallback - AllowAnyOrigin is Strictly Prohibited)
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? [];

if (allowedOrigins.Length == 0 && builder.Environment.IsProduction())
{
    throw new InvalidOperationException("Zero-Trust Security Violation: 'AllowedOrigins' must contain at least one explicit origin in production. Open CORS is strictly prohibited.");
}

builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var origins = configuration.GetSection("AllowedOrigins").Get<string[]>();
    if (origins == null || origins.Length == 0)
    {
        origins = configuration.GetSection("AllowedOrigins").GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrEmpty(v)).ToArray()!;
    }
    if (origins == null || origins.Length == 0)
    {
        origins = allowedOrigins;
    }

    options.AddDefaultPolicy(policy =>
    {
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                .WithMethods("POST", "GET", "PUT", "DELETE", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id", "X-Tenant-Id", "X-License-Key")
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        }
        // Zero fallback: If no origins are configured, the policy admits no cross-origin traffic (fails closed).
    });
});

// 8. Dual-Tier Rate Limiting (Global Sliding Window + Expensive Operations Fixed Window)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("api-global", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));

    options.AddPolicy("api-expensive", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Tenant-Level Partitioned Rate Limiter: prevents noisy neighbor issues across tenants
    options.AddPolicy("api-tenant", httpContext =>
    {
        var tenantId = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? httpContext.User.FindFirst("tenant_id")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous-tenant";

        return RateLimitPartition.GetTokenBucketLimiter(
            tenantId,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 200,
                TokensPerPeriod = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                AutoReplenishment = true,
                QueueLimit = 0
            });
    });
});

// 9. Add Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();


// 10. Middleware Pipeline
app.UseHttpsRedirection();
app.UseCors();
app.UseMiddleware<EnforceMandatoryCorrelationIdMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<EnforceMandatoryTenantAndAuthMiddleware>();
app.UseAuthorization();

// 11. Minimal API Route Groups
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapProcessEndpoints();

// 12. Health Check & Diagnostic Probes (Public exemptions)
app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapHealthChecks("/alive").AllowAnonymous();

app.MapGet("/", (ICorrelationContext context) => new
{
    service = "BlazorEnterprise ProcessAPI & Centralized Auth Gateway",
    version = "1.0.0",
    status = "Healthy",
    correlationId = context.CorrelationId,
    timestamp = DateTime.UtcNow
}).RequireCors().AllowAnonymous();

app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
