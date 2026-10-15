using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using BlazorFluent.Api.Admin;
using BlazorFluent.Api.Auth;
using BlazorFluent.Api.Endpoints;
using BlazorFluent.Api.Middleware;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Shared Serilog setup
builder.Host.UseSharedSerilog("BlazorFluent.Api");

// 2. Add endpoints exploration
builder.Services.AddEndpointsApiExplorer();

// 3. Register Correlation Context (ambient scoped context)
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();

// 4. Register Multi-IdP Auth Gateway and Entitlements
builder.Services.AddSingleton<IEntitlementsRepository, InMemoryEntitlementsRepository>();
builder.Services.AddScoped<IAuthGatewayService, AuthGatewayService>();
builder.Services.AddScoped<IAdminSecurityService, AdminSecurityService>();

// 5. JWT Bearer Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Auth:Issuer"] ?? "https://auth.company.com";
        var audience = builder.Configuration["Auth:Audience"] ?? "https://api.company.com";
        var secret = builder.Configuration["Auth:SigningKey"] ?? "BlazorFluent-Cloud-AuthGateway-MasterKey-2026-Secure!";
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
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

// 7. Restrictive CORS Policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? [];

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .WithMethods("POST", "GET", "PUT", "DELETE", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id", "X-Tenant-Id", "X-License-Key")
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        }
        else
        {
            policy.AllowAnyOrigin()
                .WithMethods("POST", "GET", "PUT", "DELETE", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id", "X-Tenant-Id", "X-License-Key");
        }
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
    service = "BlazorFluent ProcessAPI & Centralized Auth Gateway",
    version = "1.0.0",
    status = "Healthy",
    correlationId = context.CorrelationId,
    timestamp = DateTime.UtcNow
}).AllowAnonymous();

app.Run();
