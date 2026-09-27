namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Middleware extensions for injecting HTTP security headers into all responses.
/// Hardens the internet-facing Blazor WebApp against clickjacking, MIME sniffing, and cross-origin leaks.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            // 1. Clickjacking protection: Disallow embedding inside third-party iframes
            if (!headers.ContainsKey("X-Frame-Options"))
            {
                headers.Append("X-Frame-Options", "SAMEORIGIN");
            }

            // 2. MIME sniffing protection: Force browsers to adhere to declared Content-Type
            if (!headers.ContainsKey("X-Content-Type-Options"))
            {
                headers.Append("X-Content-Type-Options", "nosniff");
            }

            // 3. Referrer leakage prevention: Send origin only on cross-origin requests
            if (!headers.ContainsKey("Referrer-Policy"))
            {
                headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            }

            // 4. Feature and device API restriction: Disable unused hardware capabilities
            if (!headers.ContainsKey("Permissions-Policy"))
            {
                headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            }

            // 5. Legacy XSS filter protection
            if (!headers.ContainsKey("X-XSS-Protection"))
            {
                headers.Append("X-XSS-Protection", "1; mode=block");
            }

            await next();
        });
    }
}
