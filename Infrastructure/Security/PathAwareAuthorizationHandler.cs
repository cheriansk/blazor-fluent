using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Custom authorization middleware result handler inspired by FullStackHero.
/// Allows public system paths (error pages, static assets, favicon) to bypass
/// strict DefaultPolicy/FallbackPolicy gates so assets and error templates render cleanly.
/// </summary>
public class PathAwareAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    private static readonly string[] BypassPrefixes =
    [
        "/_content",
        "/_framework",
        "/login",
        "/auth-error",
        "/authentication",
        "/Error",
        "/not-found",
        "/favicon.ico",
        "/healthz",
        "/health/"
    ];

    private static readonly string[] BypassExtensions =
    [
        ".css",
        ".js",
        ".png",
        ".svg",
        ".ico",
        ".woff",
        ".woff2"
    ];

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var path = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path))
        {
            if (BypassPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ||
                BypassExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                await next(context);
                return;
            }
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
