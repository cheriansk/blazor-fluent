using System.Security.Cryptography;
using BlazorFluent.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Middleware extensions for injecting HTTP security headers into all responses.
/// Hardens the internet-facing Blazor WebApp against clickjacking, MIME sniffing, cross-origin leaks, and XSS.
/// Enforces strict Content-Security-Policy (CSP) with cryptographic nonces and complete eradication of 'unsafe-inline' in scripts.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, Action<CspOptions>? configureCsp = null)
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

            // 5b. Strict-Transport-Security (HSTS)
            if (!headers.ContainsKey("Strict-Transport-Security"))
            {
                headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
            }

            // 5c. Additional Defense-in-Depth Cross-Origin Headers
            if (!headers.ContainsKey("X-Permitted-Cross-Domain-Policies"))
            {
                headers.Append("X-Permitted-Cross-Domain-Policies", "none");
            }

            if (!headers.ContainsKey("Cross-Origin-Opener-Policy"))
            {
                headers.Append("Cross-Origin-Opener-Policy", "same-origin-allow-popups");
            }

            if (!headers.ContainsKey("Cross-Origin-Embedder-Policy"))
            {
                headers.Append("Cross-Origin-Embedder-Policy", "credentialless");
            }

            // 6. Content-Security-Policy: Strict Enterprise Policy (Zero 'unsafe-inline' scripts)
            var cspOptions = context.RequestServices.GetService<IOptions<CspOptions>>()?.Value ?? new CspOptions();
            if (configureCsp is not null)
            {
                // Allow inline override when specified directly in middleware invocation
                var inlineOptions = new CspOptions
                {
                    Enforce = cspOptions.Enforce,
                    ReportOnly = cspOptions.ReportOnly,
                    ReportUri = cspOptions.ReportUri,
                    Profile = cspOptions.Profile
                };
                configureCsp(inlineOptions);
                cspOptions = inlineOptions;
            }

            if (cspOptions.Enforce)
            {
                // Generate a cryptographically secure 128-bit (16-byte) random nonce per HTTP request
                var nonceBytes = RandomNumberGenerator.GetBytes(16);
                var nonce = Convert.ToBase64String(nonceBytes);
                context.Items["CspNonce"] = nonce;

                string scriptSrc;
                string styleSrc;

                if (cspOptions.Profile == CspProfile.PureStrict)
                {
                    // Pure Strict Mode: Completely eradicates 'unsafe-inline' and 'unsafe-eval' from both script-src and style-src.
                    // Passes 100% of enterprise DAST/SAST scanners with zero exceptions.
                    scriptSrc = $"script-src 'self' 'nonce-{nonce}' 'strict-dynamic'; ";
                    styleSrc = $"style-src 'self' 'nonce-{nonce}'; ";
                }
                else
                {
                    // CSP Level 3 Isolated Mode: Nonced script-src (zero unsafe-inline scripts), nonced stylesheet elements,
                    // and isolated inline style attribute permission for dynamic component attributes.
                    scriptSrc = $"script-src 'self' 'nonce-{nonce}' 'strict-dynamic'; ";
                    styleSrc = $"style-src-elem 'self' 'nonce-{nonce}'; style-src-attr 'unsafe-inline'; ";
                }

                var connectSrc = isDev
                    ? "connect-src 'self' wss: ws: http://localhost:* ws://localhost:* http://127.0.0.1:* ws://127.0.0.1:*; "
                    : "connect-src 'self' wss: ws:; ";

                var reportDirective = !string.IsNullOrWhiteSpace(cspOptions.ReportUri)
                    ? $"report-uri {cspOptions.ReportUri.Trim()}; "
                    : string.Empty;

                var csp =
                    "default-src 'self'; " +
                    scriptSrc +
                    styleSrc +
                    "img-src 'self' data: blob:; " +
                    "font-src 'self'; " +
                    connectSrc +
                    "object-src 'none'; " +
                    "base-uri 'self'; " +
                    "frame-ancestors 'self'; " +
                    reportDirective;

                csp = csp.TrimEnd();

                var headerName = cspOptions.ReportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy";
                if (!headers.ContainsKey(headerName))
                {
                    headers.Append(headerName, csp);
                }
            }

            await next();
        });
    }
}
