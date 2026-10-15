using BlazorFluent.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BlazorFluent.Api.Endpoints;

public record AuthorizeRequest(string Email, string ClientId, string RedirectUri);
public record TokenRequest(string Code, string ClientId, string RedirectUri);

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .AllowAnonymous()
            .RequireRateLimiting("api-expensive");

        group.MapPost("/authorize", async (
            [FromBody] AuthorizeRequest request,
            IAuthGatewayService authGateway,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Auth");
            logger.LogInformation("Processing authorization code request for client {ClientId}", request.ClientId);

            var result = await authGateway.GenerateAuthorizationCodeAsync(request.Email, request.ClientId);
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new { error = "AuthorizationFailed", details = result.Errors });
            }

            return Results.Ok(new { code = result.Data, redirectUri = request.RedirectUri });
        })
        .WithName("AuthorizeClient")
        .WithSummary("Generate single-use authorization code for provisioned identity");

        group.MapPost("/token", async (
            [FromBody] TokenRequest request,
            IAuthGatewayService authGateway,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Auth");
            logger.LogInformation("Processing token exchange request for client {ClientId}", request.ClientId);

            var result = await authGateway.ExchangeCodeForTokenAsync(request.Code, request.ClientId);
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new { error = "TokenExchangeFailed", details = result.Errors });
            }

            return Results.Ok(result.Data);
        })
        .WithName("ExchangeAuthToken")
        .WithSummary("Exchange authorization code for signed JWT bearer token");

        return group;
    }
}
