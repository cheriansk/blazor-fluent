using BlazorFluent.Core.Contracts;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorFluent.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Read timezone configuration (default is UTC)
        var useUtc = !bool.TryParse(configuration["DateTimeSettings:UseUtc"], out var parsed) || parsed;
        services.TryAddSingleton<IDateTimeProvider>(new ConfigurableDateTimeProvider(useUtc));

        // 2. Register current user and tenant context (scoped per request/circuit)
        services.TryAddScoped<ICurrentUser, DefaultCurrentUser>();
        services.TryAddScoped<ITenantContext, TenantContext>();

        // 3. Register audit interceptor (scoped — needs ICurrentUser + ITenantContext which are scoped)
        services.AddScoped<AuditableEntityInterceptor>();

        // 4. Strict connection string loading from appsettings.json
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' was not found or is empty in configuration (appsettings.json). " +
                "Please configure 'ConnectionStrings:DefaultConnection' in appsettings.json.");
        }

        // 5. Register AppDbContext with Npgsql and the audit interceptor
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            })
            .AddInterceptors(interceptor);
        });

        return services;
    }
}
