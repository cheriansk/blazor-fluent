using System.Security.Claims;
using BlazorEnterprise.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BlazorEnterprise.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin")
            .WithTags("Administration")
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("api-global");

        group.MapGet("/entitlements", async (
            IEntitlementsRepository entitlementsRepo,
            ClaimsPrincipal principal,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorEnterprise.Api.Endpoints.Admin");
            var adminEmail = GetAdminEmail(principal);
            logger.LogInformation("Administrator {AdminEmail} requested all entitlements", adminEmail);

            var users = await entitlementsRepo.GetAllAsync();
            return Results.Ok(users);
        })
        .WithName("GetEntitlements")
        .WithSummary("Retrieve all provisioned user entitlements");

        group.MapPost("/entitlements", async (
            [FromBody] EntitlementUser user,
            IEntitlementsRepository entitlementsRepo,
            ClaimsPrincipal principal,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorEnterprise.Api.Endpoints.Admin");
            var adminEmail = GetAdminEmail(principal);

            if (string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.TenantId))
            {
                return Results.BadRequest(new { error = "ValidationError", message = "Email and TenantId are required fields." });
            }

            await entitlementsRepo.UpsertAsync(user);
            logger.LogInformation("Administrator {AdminEmail} upserted entitlement for {TargetUser}", adminEmail, user.Email);

            return Results.Ok(user);
        })
        .WithName("UpsertEntitlement")
        .WithSummary("Create or update a tenant user entitlement");

        group.MapDelete("/entitlements/{targetEmail}", async (
            string targetEmail,
            IEntitlementsRepository entitlementsRepo,
            ClaimsPrincipal principal,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorEnterprise.Api.Endpoints.Admin");
            var adminEmail = GetAdminEmail(principal);

            var removed = await entitlementsRepo.DeleteAsync(targetEmail);
            logger.LogInformation("Administrator {AdminEmail} deleted entitlement for {TargetUser} (Result: {Removed})",
                adminEmail, targetEmail, removed);

            return Results.Ok(new { removed });
        })
        .WithName("DeleteEntitlement")
        .WithSummary("Delete a provisioned tenant user entitlement");

        return group;
    }

    private static string GetAdminEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.Identity?.Name
            ?? "Admin";
    }
}
