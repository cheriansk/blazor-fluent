using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using BlazorEnterprise.Shared.Common;
using Microsoft.AspNetCore.Authorization;

namespace BlazorEnterprise.Api.Middleware;

/// <summary>
/// Level 3 API Zero-Trust Guardrail: Enforces that every non-anonymous HTTP request
/// is either:
/// 1. An Authenticated Tenant User (possessing both a verified user identity claim AND an active 'tenant_id'/'tid' claim), OR
/// 2. A Tenant-Scoped SystemDaemon S2S worker token (signed with the tenant's cryptographic license key, possessing 'act' and 'tenant_id'/'tid' claims).
/// </summary>
public class EnforceMandatoryTenantAndAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnforceMandatoryTenantAndAuthMiddleware> _logger;

    public EnforceMandatoryTenantAndAuthMiddleware(
        RequestDelegate next,
        ILogger<EnforceMandatoryTenantAndAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Exempt CORS preflight, health checks, and static assets
        if (HttpMethods.IsOptions(context.Request.Method) ||
            path.StartsWith("/health") || path.StartsWith("/alive") ||
            path.EndsWith(".ico") || path.EndsWith(".css") || path.EndsWith(".js"))
        {
            await _next(context);
            return;
        }

        // Exempt endpoints explicitly marked [AllowAnonymous] (e.g. /api/v1/auth/authorize, /api/v1/auth/token, /)
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var tenantId = user.FindFirstValue("tenant_id")
            ?? user.FindFirstValue("tid");

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("uid")
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        // Cryptographically signed S2S daemon tokens carry the 'act' (actor capability) claim and 'tenant_id'/'tid'
        var daemonActorCapability = user.FindFirstValue("act");

        var hasActiveTenant = !string.IsNullOrWhiteSpace(tenantId);
        var isAuthenticatedTenantUser = hasActiveTenant && !string.IsNullOrWhiteSpace(userId);
        var isTenantScopedSystemDaemon = hasActiveTenant && !string.IsNullOrWhiteSpace(daemonActorCapability);

        if (!isAuthenticatedTenantUser && !isTenantScopedSystemDaemon)
        {
            _logger.LogCritical(
                "CRITICAL SECURITY VIOLATION: API request to '{Path}' rejected by Level 3 Tenant/Auth Guardrail. IsAuthenticatedTenantUser={IsTenantUser}, IsSystemDaemon={IsDaemon}, HasTenant={HasTenant}.",
                context.Request.Path,
                isAuthenticatedTenantUser,
                isTenantScopedSystemDaemon,
                hasActiveTenant);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var errorPayload = new
            {
                error = "UnauthorizedTenantContext",
                message = "Zero-Trust Security Violation: Every protected API request requires an authenticated tenant user or tenant-scoped S2S daemon token.",
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorPayload, JsonDefaults.StandardOptions));
            return;
        }

        await _next(context);
    }
}
