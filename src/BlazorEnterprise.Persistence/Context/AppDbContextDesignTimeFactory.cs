using System;
using System.IO;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using TenantInfo = BlazorEnterprise.Core.Contracts.TenantInfo;

namespace BlazorEnterprise.Persistence.Context;

/// <summary>
/// Design-time factory used by EF Core command-line tools (dotnet ef) to scaffold migrations
/// dynamically deriving connection strings and Root Tenant context from configuration.
/// Zero-Trust: Fails fast if configuration is missing (zero hardcoded credentials or GUIDs).
/// </summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true);

        // Also probe BlazorEnterprise.Web project directory if executing from solution or persistence directory
        var candidateWebPaths = new[]
        {
            Path.Combine(basePath, "..", "BlazorEnterprise.Web"),
            Path.Combine(basePath, "src", "BlazorEnterprise.Web"),
            Path.Combine(basePath, "..", "..", "src", "BlazorEnterprise.Web")
        };

        foreach (var webPath in candidateWebPaths)
        {
            if (Directory.Exists(webPath))
            {
                configBuilder.AddJsonFile(Path.Combine(webPath, "appsettings.json"), optional: true);
                configBuilder.AddJsonFile(Path.Combine(webPath, "appsettings.Development.json"), optional: true);
                break;
            }
        }

        configBuilder.AddEnvironmentVariables();

        try
        {
            configBuilder.AddUserSecrets(typeof(AppDbContextDesignTimeFactory).Assembly, optional: true);
        }
        catch
        {
            // Optional user-secrets fallback
        }

        var configuration = configBuilder.Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings:DefaultConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Zero-Trust Security Violation: 'ConnectionStrings:DefaultConnection' configuration is missing for design-time migrations. " +
                "Ensure it is configured via User Secrets, Environment Variables (ConnectionStrings__DefaultConnection), or appsettings.");
        }

        var rawRootTenantId = configuration["Admin:RootTenantId"];
        if (string.IsNullOrWhiteSpace(rawRootTenantId) || !Guid.TryParse(rawRootTenantId, out var rootTenantId) || rootTenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Zero-Trust Security Violation: Configuration 'Admin:RootTenantId' is required and must be a valid non-empty GUID for design-time migrations (Fail-Closed). " +
                "Configure it via User Secrets or Environment Variables (Admin__RootTenantId).");
        }

        var rootTenantSlug = configuration["Admin:RootTenantSlug"];
        if (string.IsNullOrWhiteSpace(rootTenantSlug))
        {
            rootTenantSlug = "root";
        }

        var rootTenantName = configuration["Admin:RootTenantName"];
        if (string.IsNullOrWhiteSpace(rootTenantName))
        {
            rootTenantName = "System Administration";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        });

        var dateTimeProvider = new ConfigurableDateTimeProvider(useUtc: true);
        var tenantContext = new TenantContext();
        tenantContext.Initialize(
            tenantId: rootTenantId,
            tenantName: rootTenantName,
            userType: UserTypeEnum.CompanyUser,
            allowedTenants: new[] { new TenantInfo(rootTenantId, rootTenantSlug, rootTenantName, true) },
            tenantSlug: rootTenantSlug,
            isRootTenant: true);

        optionsBuilder.AddInterceptors(new TenantDbConnectionInterceptor(tenantContext));

        return new AppDbContext(
            optionsBuilder.Options,
            dateTimeProvider,
            tenantContext,
            serviceProvider: null);
    }
}
