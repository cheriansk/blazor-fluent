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
/// Endpoint filter extension enforcing hierarchical clearance tiers on Minimal API endpoints.
/// Evaluates user claims and emits Zero-PII security audit logs on 403 Forbidden.
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

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            // Root / Host Admin bypass
            var isRoot = user.IsInRole("RootAdmin") || user.IsInRole("HostAdmin") || user.HasClaim("is_root", "true");
            if (isRoot)
            {
                return await next(context);
            }

            // Extract role claims and find highest assigned clearance
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
                else if (Enum.TryParse<ProjectRoleEnum>(r, ignoreCase: true, out var parsedRole))
                {
                    if ((int)parsedRole > highestTier) highestTier = (int)parsedRole;
                }
            }

            if (highestTier < (int)minTier)
            {
                var tenantId = user.FindFirstValue("tenant_id") ?? user.FindFirstValue("tid") ?? "unknown";
                // Zero-PII Warning logging (strictly GUIDs, zero emails)
                logger.LogWarning(
                    "AUDIT [Security]: API clearance denied. UserId='{UserId}', TenantId='{TenantId}', Route='{Route}', RequiredTier='{RequiredTier}', ActualTier='{ActualTier}'",
                    userId, tenantId, httpContext.Request.Path, (int)minTier, highestTier);

                return Results.Forbid();
            }

            return await next(context);
        });
    }
}
