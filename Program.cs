using BlazorFluent.Components;
using BlazorFluent.Jobs;
using BlazorFluent.Persistence;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure console logging timezone based on DateTimeSettings:UseUtc
var useUtc = !bool.TryParse(builder.Configuration["DateTimeSettings:UseUtc"], out var parsedUtc) || parsedUtc;
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = useUtc;
    options.TimestampFormat = useUtc ? "[yyyy-MM-dd HH:mm:ss UTC] " : "[yyyy-MM-dd HH:mm:ss] ";
});

// 2. Add presentation and UI services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

// 3. Lean Modular Monolith Registrations
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddBackgroundJobs(enableScheduler: true);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


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