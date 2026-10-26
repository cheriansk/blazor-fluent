using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Interceptors;
using BlazorEnterprise.Persistence.Services;
using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FluentValidation;
using BlazorEnterprise.Persistence.Validation;

namespace BlazorEnterprise.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Read timezone configuration (default is UTC)
        var useUtc = true;//!bool.TryParse(configuration["DateTimeSettings:UseUtc"], out var parsed) || parsed;
        services.TryAddSingleton<IDateTimeProvider>(new ConfigurableDateTimeProvider(useUtc));

        // 2. Register current user, tenant context, project context, and unsaved changes state (scoped per request/circuit)
        services.TryAddScoped<ICurrentUser, DefaultCurrentUser>();
        services.TryAddScoped<ITenantContext, TenantContext>();
        services.TryAddScoped<IProjectContext, ProjectContext>();
        services.TryAddScoped<IUnsavedChangesStateService, UnsavedChangesStateService>();

        // 3. Register forensic audit service (scoped)
        services.TryAddScoped<IAuditService, AuditService>();

        // 4. Register interceptors (Singletons — thread-safe, dynamically read context from AppDbContext)
        services.AddSingleton<ZeroTrustDbCommandInterceptor>();
        services.AddSingleton(_ => new TenantDbConnectionInterceptor());
        services.AddSingleton(_ => new EntityValidationInterceptor());
        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddSingleton<ProjectSecurityInterceptor>();
        services.AddSingleton<NoTrackingMutationGuardInterceptor>();

        // 4b. Register DataProtectionKey entity validator for fail-closed Tier 2 entity validation
        services.AddSingleton<IValidator<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>, DataProtectionKeyValidator>();

        // 5. Strict connection string loading and NpgsqlDataSource registration with connection pooling
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' was not found or is empty in configuration (appsettings.json). " +
                "Please configure 'ConnectionStrings:DefaultConnection' in appsettings.json.");
        }

        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
        var npgsqlDataSource = dataSourceBuilder.Build();
        services.AddSingleton(npgsqlDataSource);

        // 6. Register AppDbContext and IDbContextFactory with Npgsql, validation, audit & security interceptors, and DataProtection support
        Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext = (sp, options) =>
        {
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.ConfigureWarnings(w => w
                .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)
                .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            var zeroTrustInterceptor = sp.GetRequiredService<ZeroTrustDbCommandInterceptor>();
            var connectionInterceptor = sp.GetRequiredService<TenantDbConnectionInterceptor>();
            var validationInterceptor = sp.GetRequiredService<EntityValidationInterceptor>();
            var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            var securityInterceptor = sp.GetRequiredService<ProjectSecurityInterceptor>();
            var noTrackingGuard = sp.GetRequiredService<NoTrackingMutationGuardInterceptor>();
            var dataSource = sp.GetRequiredService<Npgsql.NpgsqlDataSource>();

            options.UseExceptionProcessor();
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            })
            .AddInterceptors(zeroTrustInterceptor, connectionInterceptor, validationInterceptor, interceptor, securityInterceptor, noTrackingGuard);
        };

        services.AddDbContext<AppDbContext>(configureDbContext);
        services.AddDbContextFactory<AppDbContext>(configureDbContext, ServiceLifetime.Scoped);

        // 7. Auto-scaling Azure Web Apps: Shared Data Protection Key Ring in PostgreSQL
        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("BlazorEnterprise");

        // 8. Multi-Instance Caching (L1 In-Memory + L2 Distributed with auto-scaled invalidation)
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "BlazorEnterprise:";
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

        // 12. Register Project Authorization Service with direct live real-time queries
        services.TryAddScoped<IProjectAuthorizationService, ProjectAuthorizationService>();

        // 13. Multi-Channel Notification Engine Data Service
        services.TryAddScoped<INotifyService, NotificationService>();

        // 14. Enterprise Identity: Sessions & Impersonation
        services.TryAddScoped<IUserSessionService, UserSessionService>();
        services.TryAddScoped<IImpersonationService, ImpersonationService>();

        // 15. Project Work Item & Task Management Service
        services.TryAddScoped<IUserTaskService, UserTaskService>();

        // 16. Knowledge Management Service
        services.TryAddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

        // 17. Universal Event Tracker Ledger Service
        services.TryAddScoped<IEventTrackerService, EventTrackerService>();

        // 18. Multi-Tenant File Ingestion & Processing Pipeline Service
        services.TryAddScoped<IImportFileService, ImportFileService>();

        // 19. Zero-Trust Internal Authentication Verification Service (runs in isolated system scope)
        services.TryAddScoped<IInternalAuthenticationService, InternalAuthenticationService>();

        // 20. Project Milestones & Delivery Cadence Service
        services.TryAddScoped<IProjectMilestoneService, ProjectMilestoneService>();

        // 21. Real-Time Project & Milestone Health Engine Service
        services.TryAddScoped<IProjectHealthService, ProjectHealthService>();

        // 22. Hierarchical Authorization & Program Governance Services
        services.TryAddScoped<IHierarchyAuthorizationService, HierarchyAuthorizationService>();
        services.TryAddScoped<IProgramService, ProgramService>();

        // 23. Enterprise Work Item Rollup Providers (Plugin Architecture)
        services.AddScoped<IWorkItemRollupProvider, BlazorEnterprise.Persistence.Services.Providers.TaskRollupProvider>();
        services.AddScoped<IWorkItemRollupProvider, BlazorEnterprise.Persistence.Services.Providers.SubTaskRollupProvider>();
        services.AddScoped<IWorkItemRollupProvider, BlazorEnterprise.Persistence.Services.Providers.RiskRollupProvider>();
        services.AddScoped<IWorkItemRollupProvider, BlazorEnterprise.Persistence.Services.Providers.AssumptionRollupProvider>();
        services.AddScoped<IWorkItemRollupProvider, BlazorEnterprise.Persistence.Services.Providers.IssueRollupProvider>();
        services.AddScoped<IWorkItemRollupProvider, BlazorEnterprise.Persistence.Services.Providers.BlockerRollupProvider>();

        // 24. Unified Enterprise Calendar Service
        services.TryAddScoped<ICalendarService, CalendarService>();

        // 25. ServiceNow-Style Sequential Workflow Engine
        services.TryAddScoped<IWorkflowService, WorkflowService>();

        // 26. User Preferences & Presentation Settings Service
        services.TryAddScoped<IUserPreferencesService, UserPreferencesService>();

        return services;
    }
}
