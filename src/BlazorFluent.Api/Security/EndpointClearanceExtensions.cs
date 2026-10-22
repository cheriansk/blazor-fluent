using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Api.Security;

/// <summary>
/// Endpoint filter extension enforcing Level 3 Zero-Trust tenant user verification and hierarchical clearance tiers on Minimal API endpoints.
/// Evaluates user claims and emits Zero-PII security audit logs on 401/403.
/// </summary>
public static class EndpointClearanceExtensions
{
    public static RouteHandlerBuilder RequireClearance(this RouteHandlerBuilder builder, ProjectRoleEnum minTier)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var user = httpContext.User;
            var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BlazorFluent.Api.Security.EndpointClearance");

            if (user?.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("uid");

            var tenantId = user.FindFirstValue("tenant_id")
                ?? user.FindFirstValue("tid");

            // Level 3 Guardrail: Must have both authenticated userId and active tenantId before evaluating roles
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tenantId))
            {
                logger.LogCritical(
                    "CRITICAL SECURITY VIOLATION: API clearance check invoked without valid UserId or TenantId on route '{Route}'.",
                    httpContext.Request.Path);
                return Results.Unauthorized();
            }

            // Extract role claims and find highest assigned clearance (no unconditional Root/Host bypass)
            var roleClaims = user.FindAll(ClaimTypes.Role)
                .Concat(user.FindAll("project_role"))
                .Concat(user.FindAll("clearance_tier"))
                .Select(c => c.Value);

            int highestTier = 0;
            foreach (var r in roleClaims)
            {
                if (int.TryParse(r, out var numericTier))
                {
                    if (numericTier > highestTier) highestTier = numericTier;
                }
                else if (r.TryToEnum<ProjectRoleEnum>(out var parsedRole))
                {
                    if (parsedRole.ToInt() > highestTier) highestTier = parsedRole.ToInt();
                }
            }

            if (highestTier < minTier.ToInt())
            {
                // Zero-PII Warning logging (strictly GUIDs, zero emails)
                logger.LogWarning(
                    "AUDIT [Security]: API clearance denied. UserId='{UserId}', TenantId='{TenantId}', Route='{Route}', RequiredTier='{RequiredTier}', ActualTier='{ActualTier}'",
                    userId, tenantId, httpContext.Request.Path, minTier.ToInt(), highestTier);

                return Results.Forbid();
            }

            return await next(context);
        });
    }
}
