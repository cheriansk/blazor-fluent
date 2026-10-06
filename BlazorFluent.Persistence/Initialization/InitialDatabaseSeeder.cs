using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Initialization;

/// <summary>
/// Seeds the system on initial startup with the internal root anchor default tenant
/// and the designated root super-administrators (max 3) from configuration.
/// </summary>
public static class InitialDatabaseSeeder
{
    public const string DefaultTenantSlug = "default";
    public const string DefaultTenantCode = "DEFAULT";
    public const string DefaultTenantName = "System Root Anchor";

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        currentUser?.SetSystemDaemon("DatabaseSeeder");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rootAdminService = scope.ServiceProvider.GetRequiredService<IRootAdminService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InitialDatabaseSeeder));

        var integrityKey = configuration["Security:IntegritySecret"] ?? "BlazorFluent-Secret-Key-Change-In-Production-2026";
        var adminEmails = rootAdminService.GetRootAdminEmails();

        // 1. Ensure internal root anchor Default Tenant exists
        var defaultTenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Slug == DefaultTenantSlug, cancellationToken);

        if (defaultTenant is null)
        {
            logger.LogInformation("Provisioning internal root anchor default tenant ('{Slug}')...", DefaultTenantSlug);

            // Derive internal domains from admin emails
            var adminDomains = adminEmails
                .Select(e => e.Contains('@') ? "@" + e.Split('@')[1] : "@blazorfluent.local")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (adminDomains.Count == 0)
            {
                adminDomains.Add("@blazorfluent.local");
            }

            defaultTenant = new TenantEntity
            {
                Id = Guid.NewGuid(),
                Slug = DefaultTenantSlug,
                Code = DefaultTenantCode,
                Name = DefaultTenantName,
                InternalEmailDomains = string.Join(";", adminDomains),
                ExternalEmailDomains = "@client.com",
                IsActive = true,
                StartDate = DateTime.UtcNow.AddYears(-1),
                EndDate = DateTime.UtcNow.AddYears(20)
            };

            dbContext.Tenants.Add(defaultTenant);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Internal root anchor default tenant provisioned successfully.");
        }

        // 2. Seed up to 3 Root Administrator accounts inside the default tenant
        var existingCount = await dbContext.Users
            .CountAsync(u => u.DefaultTenantId == DefaultTenantSlug && !u.IsDeleted, cancellationToken);

        var index = 1;
        var usersAdded = false;

        foreach (var email in adminEmails)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var existingUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

            if (existingUser is null)
            {
                if (existingCount >= 3)
                {
                    logger.LogWarning(
                        "Default tenant already contains {Count} users. Cannot add root admin '{Email}'. Hard limit of 3 enforced.",
                        existingCount, normalizedEmail);
                    break;
                }

                logger.LogInformation("Seeding Root Administrator {Index} with email '{Email}'...", index, normalizedEmail);

                var user = new UserEntity
                {
                    Id = Guid.NewGuid(),
                    Email = normalizedEmail,
                    FullName = $"Root Administrator {index}",
                    UserType = UserType.CompanyUser,
                    DefaultTenantId = DefaultTenantSlug,
                    IsActive = true,
                    StartDateUtc = DateTime.UtcNow.AddYears(-1),
                    EndDateUtc = DateTime.UtcNow.AddYears(20)
                };

                user.RowSignature = user.ComputeIntegritySignature(integrityKey);
                dbContext.Users.Add(user);
                existingCount++;
                usersAdded = true;
            }

            index++;
        }

        if (usersAdded)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Root super-administrators seeded successfully in default tenant.");
        }
    }
}
