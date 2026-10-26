using System.Text.RegularExpressions;
using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Security;

/// <summary>
/// Unit tests verifying strict Content-Security-Policy (CSP) enforcement:
/// Complete elimination of 'unsafe-inline' and 'unsafe-eval' from script execution,
/// per-request cryptographic nonces, and CSP Level 3 style protection.
/// </summary>
public class StrictCspSecurityHeadersTests
{
    [Fact]
    public async Task PureStrict_EradicatesUnsafeInlineAndEvalFromAllDirectives()
    {
        var options = new CspOptions
        {
            Enforce = true,
            Profile = CspProfile.PureStrict
        };

        var context = await InvokeSecurityHeadersMiddlewareAsync(options);

        var headers = context.Response.Headers;
        headers.ContainsKey("Content-Security-Policy").Should().BeTrue();

        var csp = headers["Content-Security-Policy"].ToString();

        // 1. InfoSec Zero-Tolerance: 'unsafe-inline' and 'unsafe-eval' MUST NOT appear anywhere in the header
        csp.Should().NotContain("unsafe-inline", "enterprise InfoSec strictly forbids 'unsafe-inline'");
        csp.Should().NotContain("unsafe-eval", "enterprise InfoSec strictly forbids 'unsafe-eval'");

        // 2. Strict Nonce: script-src and style-src must contain cryptographic nonce
        csp.Should().MatchRegex(@"script-src 'self' 'nonce-[A-Za-z0-9+/=]+' 'strict-dynamic';");
        csp.Should().MatchRegex(@"style-src 'self' 'nonce-[A-Za-z0-9+/=]+';");

        // 3. HttpContext.Items must store the generated nonce for razor rendering
        context.Items.ContainsKey("CspNonce").Should().BeTrue();
        var nonce = context.Items["CspNonce"]?.ToString();
        nonce.Should().NotBeNullOrWhiteSpace();
        csp.Should().Contain($"'nonce-{nonce}'");
    }

    [Fact]
    public async Task Level3Isolated_EliminatesUnsafeInlineFromScriptSrc_AndNoncesStyleElem()
    {
        var options = new CspOptions
        {
            Enforce = true,
            Profile = CspProfile.Level3Isolated
        };

        var context = await InvokeSecurityHeadersMiddlewareAsync(options);

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();

        // script-src must have ZERO unsafe-inline
        var scriptSrcPart = Regex.Match(csp, @"script-src[^;]+").Value;
        scriptSrcPart.Should().NotContain("unsafe-inline");
        scriptSrcPart.Should().NotContain("unsafe-eval");
        scriptSrcPart.Should().MatchRegex(@"'nonce-[A-Za-z0-9+/=]+'");

        // style-src-elem must be nonced, with style-src-attr permitted for inline component attributes
        csp.Should().MatchRegex(@"style-src-elem 'self' 'nonce-[A-Za-z0-9+/=]+';");
        csp.Should().Contain("style-src-attr 'unsafe-inline';");
    }

    [Fact]
    public async Task CryptographicNonce_IsUniqueAndRandomPerRequest()
    {
        var options = new CspOptions { Enforce = true };

        var context1 = await InvokeSecurityHeadersMiddlewareAsync(options);
        var context2 = await InvokeSecurityHeadersMiddlewareAsync(options);

        var nonce1 = context1.Items["CspNonce"]?.ToString();
        var nonce2 = context2.Items["CspNonce"]?.ToString();

        nonce1.Should().NotBeNullOrWhiteSpace();
        nonce2.Should().NotBeNullOrWhiteSpace();
        nonce1.Should().NotBe(nonce2, "each HTTP request must receive a distinct 128-bit cryptographic nonce");

        // Verify 128-bit (16-byte) base64 encoding (16 bytes = 24 base64 chars with padding)
        var bytes1 = Convert.FromBase64String(nonce1!);
        bytes1.Length.Should().Be(16, "128-bit nonce equals exactly 16 random bytes");
    }

    [Fact]
    public async Task ReportOnlyMode_EmitsContentSecurityPolicyReportOnlyHeader()
    {
        var options = new CspOptions
        {
            Enforce = true,
            ReportOnly = true
        };

        var context = await InvokeSecurityHeadersMiddlewareAsync(options);

        context.Response.Headers.ContainsKey("Content-Security-Policy-Report-Only").Should().BeTrue();
        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
    }

    [Fact]
    public async Task ReportUri_IsAppendedWhenConfigured()
    {
        var options = new CspOptions
        {
            Enforce = true,
            ReportUri = "/api/security/csp-violations"
        };

        var context = await InvokeSecurityHeadersMiddlewareAsync(options);

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("report-uri /api/security/csp-violations;");
    }

    [Fact]
    public async Task DefenseInDepth_StandardSecurityHeadersEnforced()
    {
        var context = await InvokeSecurityHeadersMiddlewareAsync();

        var headers = context.Response.Headers;
        headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");

        var csp = headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("object-src 'none';");
        csp.Should().Contain("base-uri 'self';");
        csp.Should().Contain("frame-ancestors 'self';");
    }

    private static async Task<HttpContext> InvokeSecurityHeadersMiddlewareAsync(
        CspOptions? options = null,
        Action<CspOptions>? inlineConfig = null,
        bool isDevelopment = false)
    {
        var services = new ServiceCollection();
        if (options != null)
        {
            services.AddSingleton(Options.Create(options));
        }

        var env = new TestHostEnvironment { EnvironmentName = isDevelopment ? Environments.Development : Environments.Production };
        services.AddSingleton<IHostEnvironment>(env);

        var serviceProvider = services.BuildServiceProvider();

        var appBuilder = new ApplicationBuilder(serviceProvider);
        appBuilder.UseSecurityHeaders(inlineConfig);
        var pipeline = appBuilder.Build();

        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        await pipeline(context);
        return context;
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "BlazorEnterprise.Web";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
