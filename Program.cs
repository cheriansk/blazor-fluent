using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using BlazorFluent.Components;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Validation;
using BlazorFluent.Infrastructure;
using BlazorFluent.Infrastructure.Observability;
using BlazorFluent.Infrastructure.Security;
using BlazorFluent.Jobs;
using BlazorFluent.Persistence;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.FluentUI.AspNetCore.Components;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using System.Threading.RateLimiting;

// 1. Serilog Two-Stage Bootstrapping (captures early startup crashes)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BlazorFluent application host...");

    var builder = WebApplication.CreateBuilder(args);

    // Enforce DI scope validation & build validation across all environments to fail fast on captive dependencies
    builder.Host.UseDefaultServiceProvider((context, options) =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });

    // Azure Key Vault configuration source for production environments
    if (builder.Environment.IsProduction())
    {
        var keyVaultUri = builder.Configuration["KeyVault:Uri"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());
            Log.Information("Azure Key Vault configuration source registered: {Uri}", keyVaultUri);
        }
        else
        {
            Log.Warning("KeyVault:Uri not configured — running without Azure Key Vault in production.");
        }
    }

    // 2. Configure Serilog using Host Integration, appsettings.json, and Timezone Settings
    var useUtc = !bool.TryParse(builder.Configuration["DateTimeSettings:UseUtc"], out var parsedUtc) || parsedUtc;
    var timestampFormat = useUtc ? "yyyy-MM-dd HH:mm:ss 'UTC'" : "yyyy-MM-dd HH:mm:ss";
    var logOutputTemplate = $"[{{Timestamp:{timestampFormat}}}] [{{Level:u3}}] {{Message:lj}} {{Properties:j}}{{NewLine}}{{Exception}}";

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console(outputTemplate: logOutputTemplate)
            .WriteTo.File(
                path: "logs/blazorfluent-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: logOutputTemplate);
    });

    // 3. Add presentation and UI services
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddFluentUIComponents();
    builder.Services.AddScoped<INavigationStateService, NavigationStateService>();

    // 4. Authentication & Authorization State Provider with Dual Policy Wiring (FSH Standard)
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization(options =>
    {
        var defaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        options.DefaultPolicy = defaultPolicy;
    });
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<AppCurrentUser>();
    builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<AppCurrentUser>());
    builder.Services.AddScoped<CurrentUserAuthenticationStateProvider>();
    builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CurrentUserAuthenticationStateProvider>());

    // 5. Validation Architecture: Register all Tier 1 (Page) and Tier 2 (Entity) validators from Core
    builder.Services.AddValidatorsFromAssemblyContaining<BaseEntity>();

    // 6. Modular Monolith Registrations (Persistence, Infrastructure, Jobs)
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddBackgroundJobs(enableScheduler: true);

    // 7. Security Hardening: Request Body Limits (Anti-Pattern 5), Rate Limiting (Anti-Pattern 7) & Secure Cookie Policy
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB limit
    });

    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB limit
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("login", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 10;
            opt.QueueLimit = 0;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    builder.Services.AddCookiePolicy(options =>
    {
        options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
        options.Secure = CookieSecurePolicy.Always;
        options.MinimumSameSitePolicy = SameSiteMode.Strict;
    });

    // 8. Health Checks: Liveness (/healthz) + Readiness (/health/ready)
    builder.Services.AddHealthChecks();

    // 9. Observability: OpenTelemetry → Azure Monitor (Application Insights)
    var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(aiConnectionString))
    {
        builder.Services.AddOpenTelemetry()
            .UseAzureMonitor(options =>
            {
                options.ConnectionString = aiConnectionString;
            });
        Log.Information("OpenTelemetry → Azure Monitor configured.");
    }
    else
    {
        Log.Warning("ApplicationInsights:ConnectionString is not configured. OpenTelemetry tracing disabled.");
    }

    var app = builder.Build();

    // 10. Database Migration and Root Seeding in Development (creates tables and seeds root anchor)
    if (app.Environment.IsDevelopment())
    {
        try
        {
            Log.Information("Applying EF Core database migrations in Development...");
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            Log.Information("Database migrations applied successfully.");

            Log.Information("Seeding default root tenant anchor and designated super-administrators...");
            await BlazorFluent.Persistence.Initialization.InitialDatabaseSeeder.SeedAsync(app.Services);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to apply database migrations or seed initial data on startup. Please ensure PostgreSQL is running and connection string 'DefaultConnection' is valid.");
            throw;
        }
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    // 6. Reverse Proxy & Forwarded Headers (preserves real client IP and HTTPS scheme behind Azure App Service / Cloudflare)
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);

    app.UseCookiePolicy();

    app.UseHttpsRedirection();

    // 7. HTTP Security Headers (Clickjacking, MIME sniffing, and cross-origin protection)
    app.UseSecurityHeaders();

    // 8. Serilog HTTP Request Logging with Diagnostic Context Enrichment & Noise Filtering
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        // Silence noisy static web assets (_framework, _content, .css, .js, fonts, images)
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null || httpContext.Response.StatusCode >= 500)
                return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 400)
                return LogEventLevel.Warning;

            var path = httpContext.Request.Path.Value;
            if (path != null && (
                path.StartsWith("/_content") ||
                path.StartsWith("/_framework") ||
                path.EndsWith(".css") ||
                path.EndsWith(".js") ||
                path.EndsWith(".ico") ||
                path.EndsWith(".png") ||
                path.EndsWith(".svg") ||
                path.EndsWith(".woff2")))
            {
                return LogEventLevel.Verbose;
            }

            return LogEventLevel.Information;
        };

        // Diagnostic context enrichment: TenantId, UserId, ClientIp
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            diagnosticContext.Set("Host", httpContext.Request.Host.Value);

            var tenantContext = httpContext.RequestServices.GetService<ITenantContext>();
            if (!string.IsNullOrWhiteSpace(tenantContext?.TenantId))
            {
                diagnosticContext.Set("TenantId", tenantContext.TenantId);
            }

            var currentUser = httpContext.RequestServices.GetService<ICurrentUser>();
            if (!string.IsNullOrWhiteSpace(currentUser?.UserId))
            {
                diagnosticContext.Set("UserId", currentUser.UserId);
            }
        };
    });

    app.UseAntiforgery();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Health Check endpoints (unauthenticated probes)
    app.MapHealthChecks("/healthz", new HealthCheckOptions
    {
        Predicate = _ => false,
        AllowCachingResponses = false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        AllowCachingResponses = false
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BlazorFluent application host terminated unexpectedly.");
    throw;
}
finally
{
    Log.Information("Shutting down BlazorFluent host and flushing logs...");
    Log.CloseAndFlush();
}


/**
 * 
 * 1.  UseExceptionHandler            → RFC 9457 ProblemDetails
2.  UseResponseCompression
3.  UseCors                        ← before HTTPS redirect (preflight)
4.  UseHttpsRedirection
5.  Security headers
6.  UseStaticFiles                 (optional)
7.  Hangfire dashboard             (if jobs enabled)
8.  UseRouting
9.  OpenAPI + Scalar
10. UseAuthentication
11. Per-module ConfigureMiddleware ← multi-tenant root override etc.
12. UseRateLimiter
13. Quota enforcement              (if quotas enabled)
14. UseAuthorization
15. Per-module MapEndpoints
16. Health, SSE, SignalR
17. CurrentUserMiddleware          ← last, so authorization already done



Three ordering rules are unusual and important:

 --> Tenant resolution before authentication. Finbuckle’s strategy chain sees an anonymous User, which is why claim-aware tenant logic lives in post-auth module middleware - see the multitenancy deep dive.
--> CORS before HTTPS redirect. Preflight OPTIONS requests can’t follow redirects per the Fetch spec.
--> Per-module middleware after authentication. UseModuleMiddlewares runs each module’s ConfigureMiddleware right after UseAuthentication, so module middleware (like Multitenancy’s root-operator override) can read claims.
--> CurrentUserMiddleware runs last: it populates ICurrentUser from claims for the endpoint to consume. Anything reading ICurrentUser earlier in the chain sees nothing - middleware that needs the user reads claims directly.

Related
 * 
 */