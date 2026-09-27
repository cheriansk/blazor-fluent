using BlazorFluent.Core.Contracts;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using BlazorFluent.Persistence.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
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

        // 3. Register forensic audit service (scoped)
        services.TryAddScoped<IAuditService, AuditService>();

        // 4. Register audit interceptor (scoped — needs ICurrentUser + ITenantContext which are scoped)
        services.AddScoped<AuditableEntityInterceptor>();

        // 5. Strict connection string loading from appsettings.json
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' was not found or is empty in configuration (appsettings.json). " +
                "Please configure 'ConnectionStrings:DefaultConnection' in appsettings.json.");
        }

        // 6. Register AppDbContext with Npgsql, audit interceptor, and DataProtection support
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            })
            .AddInterceptors(interceptor);
        });

        // 7. Auto-scaling Azure Web Apps: Shared Data Protection Key Ring in PostgreSQL
        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("BlazorFluent");

        // 8. Multi-Instance Caching (L1 In-Memory + L2 Distributed with auto-scaled invalidation)
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "BlazorFluent:";
            });
        }
        else
        {
            // Default zero-dependency local development L2 cache
            services.AddDistributedMemoryCache();
        }

        var defaultExp = int.TryParse(configuration["Caching:DefaultExpirationMinutes"], out var exp) ? exp : 60;
        var localExp = int.TryParse(configuration["Caching:LocalCacheExpirationMinutes"], out var lexp) ? lexp : 15;

        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(defaultExp),
                LocalCacheExpiration = TimeSpan.FromMinutes(localExp)
            };
        });

        // 9. Register ITenantCacheService (scoped per circuit/request)
        services.TryAddScoped<ITenantCacheService, TenantHybridCacheService>();

        // 10. Register tenant administration service (scoped — depends on ITenantCacheService)
        services.TryAddScoped<ITenantService, TenantService>();

        // 11. Register Unit of Work for atomic transactions and rollbacks
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
