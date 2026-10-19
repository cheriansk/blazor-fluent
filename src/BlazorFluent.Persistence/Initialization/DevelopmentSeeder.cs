using BlazorFluent.Core.Configuration;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorFluent.Persistence.Initialization;

/// <summary>
/// Development-only database seeder driven strictly by configuration options (DevelopmentSeedOptions and DevLoginOptions).
/// Eliminates hardcoded emails, tenant slugs, and test credentials from UI components and source code.
/// </summary>
public static class DevelopmentSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var hostEnvironment = scope.ServiceProvider.GetService<IHostEnvironment>();
        if (hostEnvironment != null && !hostEnvironment.IsDevelopment())
        {
            return;
        }

        var seedOptions = scope.ServiceProvider.GetService<IOptions<DevelopmentSeedOptions>>()?.Value;
        var devLoginOptions = scope.ServiceProvider.GetService<IOptions<DevLoginOptions>>()?.Value;
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevelopmentSeeder));

        var integrityKey = configuration["Security:IntegritySecret"];
        if (string.IsNullOrWhiteSpace(integrityKey))
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        currentUser?.SetSystemDaemon("DevelopmentSeeder");

        var changesMade = false;

        // 1. Optional Custom Tenant Seeding from DevelopmentSeedOptions
        if (seedOptions != null && seedOptions.Enabled && !string.IsNullOrWhiteSpace(seedOptions.TenantSlug))
        {
            var tenantSlug = seedOptions.TenantSlug.Trim();
            var existingTenant = await dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Slug == tenantSlug, cancellationToken);

            if (existingTenant is null)
            {
                logger.LogInformation("DevelopmentSeeder: Provisioning configured dev tenant '{Slug}'...", tenantSlug);
                var tenant = new TenantEntity
                {
                    Id = Guid.NewGuid(),
                    Slug = tenantSlug,
                    Name = string.IsNullOrWhiteSpace(seedOptions.TenantName) ? tenantSlug : seedOptions.TenantName,
                    InternalEmailDomains = seedOptions.InternalEmailDomains,
                    ExternalEmailDomains = seedOptions.ExternalEmailDomains,
                    IsActive = true,
                    StartDate = DateTime.UtcNow.AddYears(-1),
                    EndDate = DateTime.UtcNow.AddYears(10)
                };
                dbContext.Tenants.Add(tenant);
                changesMade = true;
            }

            // Optional configured admin user
            if (!string.IsNullOrWhiteSpace(seedOptions.AdminEmail))
            {
                var adminEmail = seedOptions.AdminEmail.Trim().ToLowerInvariant();
                var existingAdmin = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == adminEmail && !u.IsDeleted, cancellationToken);

                if (existingAdmin is null)
                {
                    logger.LogInformation("DevelopmentSeeder: Provisioning configured dev admin '{Email}'...", adminEmail);
                    var adminUser = new UserEntity
                    {
                        Id = Guid.NewGuid(),
                        Email = adminEmail,
                        FullName = string.IsNullOrWhiteSpace(seedOptions.AdminFullName) ? "Configured Dev Admin" : seedOptions.AdminFullName,
                        UserType = UserType.CompanyUser,
                        DefaultTenantId = tenantSlug,
                        IsActive = true,
                        StartDate = DateTime.UtcNow.AddDays(-30),
                        EndDate = DateTime.UtcNow.AddYears(1),
                        TimeZoneId = "UTC"
                    };
                    adminUser.RowSignature = adminUser.ComputeIntegritySignature(integrityKey);
                    dbContext.Users.Add(adminUser);
                    changesMade = true;
                }
            }
        }

        // 2. DevLogin users seeding from DevLoginOptions
        if (devLoginOptions != null && devLoginOptions.Enabled && devLoginOptions.Users.Count > 0)
        {
            var defaultTenantSlug = seedOptions?.TenantSlug ?? InitialDatabaseSeeder.DefaultTenantSlug;

            foreach (var devUser in devLoginOptions.Users)
            {
                if (string.IsNullOrWhiteSpace(devUser.Email)) continue;

                var email = devUser.Email.Trim().ToLowerInvariant();
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);

                if (existingUser is null)
                {
                    logger.LogInformation("DevelopmentSeeder: Provisioning configured dev login persona '{Email}' ({Label})...", email, devUser.Label);
                    var user = new UserEntity
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        FullName = string.IsNullOrWhiteSpace(devUser.Label) ? email : devUser.Label,
                        UserType = UserType.CompanyUser,
                        DefaultTenantId = defaultTenantSlug,
                        IsActive = true,
                        StartDate = DateTime.UtcNow.AddDays(-30),
                        EndDate = DateTime.UtcNow.AddYears(1),
                        TimeZoneId = "UTC"
                    };
                    user.RowSignature = user.ComputeIntegritySignature(integrityKey);
                    dbContext.Users.Add(user);
                    changesMade = true;
                }
            }
        }

        if (changesMade)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("DevelopmentSeeder: Completed successfully.");
        }
    }
}
