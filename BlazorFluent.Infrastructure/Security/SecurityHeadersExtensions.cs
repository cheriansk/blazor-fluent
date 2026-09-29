using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

            var env = context.RequestServices.GetService<IHostEnvironment>();
            var isDev = env?.IsDevelopment() ?? false;

            // 4. Feature and device API restriction: Disable unused hardware capabilities
            // In development, permit unload=* to satisfy Visual Studio BrowserLink's tab-close listener
            if (!headers.ContainsKey("Permissions-Policy"))
            {
                var permissionsPolicy = isDev
                    ? "camera=(), microphone=(), geolocation=(), unload=*"
                    : "camera=(), microphone=(), geolocation=()";
                headers.Append("Permissions-Policy", permissionsPolicy);
            }

            // 5. Legacy XSS filter protection
            if (!headers.ContainsKey("X-XSS-Protection"))
            {
                headers.Append("X-XSS-Protection", "1; mode=block");
            }

            // 6. Content-Security-Policy: Blazor Server safe policy
            //    - script-src 'unsafe-inline': required for Blazor's SignalR inline bootstrap block
            //    - style-src  'unsafe-inline': required for FluentUI v5 CSS-in-JS attribute injection
            //    - connect-src: wss: / ws: for SignalR WebSocket transport (wss: in prod, ws: in dev)
            //                   In development, permits localhost dynamic ports for Visual Studio BrowserLink & Hot Reload
            //    - img-src: 'self' data: blob: for Fluent Icon SVG data URIs
            //    - object-src 'none': disables Flash / legacy embed plugins
            //    - base-uri 'self': prevents <base> tag injection attacks
            //    - frame-ancestors 'self': CSP-level clickjacking guard (supplements X-Frame-Options)
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                var connectSrc = isDev
                    ? "connect-src 'self' wss: ws: http://localhost:* ws://localhost:* http://127.0.0.1:* ws://127.0.0.1:*; "
                    : "connect-src 'self' wss: ws:; ";

                var csp =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline'; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "img-src 'self' data: blob:; " +
                    "font-src 'self'; " +
                    connectSrc +
                    "object-src 'none'; " +
                    "base-uri 'self'; " +
                    "frame-ancestors 'self';";

                headers.Append("Content-Security-Policy", csp);
            }

            await next();
        });
    }
}
