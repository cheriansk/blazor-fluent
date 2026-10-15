using BlazorFluent.Api.Admin;
using BlazorFluent.Api.Auth;
using BlazorFluent.Api.Middleware;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// 1. Shared Serilog setup
builder.Host.UseSharedSerilog("BlazorFluent.Api");

// 2. Add controllers and endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 3. Register Correlation Context (ambient scoped context)
builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();

// 4. Register Multi-IdP Auth Gateway and Entitlements
builder.Services.AddSingleton<IEntitlementsRepository, InMemoryEntitlementsRepository>();
builder.Services.AddScoped<IAuthGatewayService, AuthGatewayService>();
builder.Services.AddScoped<IAdminSecurityService, AdminSecurityService>();

// 5. Add Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// 6. Enforce Mandatory Caller-Supplied Correlation ID Middleware (Fails with 400 if missing)
app.UseMiddleware<EnforceMandatoryCorrelationIdMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/alive");

// Simple root diagnostic endpoint
app.MapGet("/", (ICorrelationContext context) => new
{
    service = "BlazorFluent ProcessAPI & Centralized Auth Gateway",
    version = "1.0.0",
    status = "Healthy",
    correlationId = context.CorrelationId,
    timestamp = DateTime.UtcNow
});

app.Run();
