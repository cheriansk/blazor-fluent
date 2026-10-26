using BlazorEnterprise.Shared.Contracts.Engine.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorEnterprise.Infrastructure.EngineGateway;

/// <summary>
/// Dependency injection wiring for outbound ProcessAPI Engine Gateway.
/// </summary>
public static class EngineGatewayExtensions
{
    public static IServiceCollection AddEngineClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Register ambient CorrelationContext if not already registered
        services.AddScoped<ICorrelationContext, CorrelationContext>();

        // Register delegating handlers
        services.AddTransient<CorrelationIdDelegatingHandler>();
        services.AddTransient<TenantAuthDelegatingHandler>();

        // Register S2S Tenant Token Service
        services.AddScoped<BlazorEnterprise.Infrastructure.Security.ITenantTokenService, BlazorEnterprise.Infrastructure.Security.TenantTokenService>();

        // Register HttpClient for ProcessAPI
        var apiBaseUrl = configuration["Engine:ApiBaseUrl"] ?? "https://localhost:7150";

        services.AddHttpClient<RemoteEngineGateway>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
        .AddHttpMessageHandler<TenantAuthDelegatingHandler>();

        // Expose as both IRemoteEngineGateway and IEngineClient
        services.AddScoped<IRemoteEngineGateway>(sp => sp.GetRequiredService<RemoteEngineGateway>());
        services.AddScoped<IEngineClient>(sp => sp.GetRequiredService<RemoteEngineGateway>());

        return services;
    }
}
