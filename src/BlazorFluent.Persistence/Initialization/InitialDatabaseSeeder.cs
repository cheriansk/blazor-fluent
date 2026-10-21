using System.Security.Cryptography;
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

namespace BlazorFluent.Persistence.Initialization;

/// <summary>
/// Seeds the system on initial startup with the internal root anchor default tenant
/// and the designated root super-administrators (max 3) from configuration.
/// </summary>
public static class InitialDatabaseSeeder
{
    public const string DefaultTenantSlug = IRootAdminService.RootTenantSlug;
    public const string DefaultTenantName = IRootAdminService.RootTenantName;

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var rootAdminService = scope.ServiceProvider.GetRequiredService<IRootAdminService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hostEnvironment = scope.ServiceProvider.GetService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InitialDatabaseSeeder));
        var isDevelopment = hostEnvironment?.IsDevelopment() ?? false;

        var integrityKey = configuration["Security:IntegritySecret"];
        if (string.IsNullOrWhiteSpace(integrityKey) || integrityKey.StartsWith("__SET_VIA_"))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: 'Security:IntegritySecret' must be configured in KeyVault, environment, or User Secrets.");
        }

        var adminEmails = rootAdminService.GetRootAdminEmails();
        if (adminEmails.Count == 0)
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: RootAdmins configuration must contain at least one valid administrator email address.");
        }

        var primaryAdminEmail = adminEmails[0].Trim().ToLowerInvariant();

        // Initialize tenant and user context for startup seeding under Root Admin identity in Root Tenant
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(IRootAdminService.RootTenantId, IRootAdminService.RootTenantSlug, IRootAdminService.RootTenantName);

        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        currentUser?.RestoreUserContext("root-seeder-bootstrap", primaryAdminEmail, "System Administrator", isRootAdmin: true);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Ensure internal root anchor Root Tenant exists
        var defaultTenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == IRootAdminService.RootTenantId || t.Slug == DefaultTenantSlug, cancellationToken);

        if (defaultTenant is null)
        {
            logger.LogInformation("Provisioning internal root anchor tenant ('{Slug}', ID: {Id})...", DefaultTenantSlug, IRootAdminService.RootTenantId);

            // Derive internal domains strictly from configured admin emails
            var adminDomains = adminEmails
                .Where(e => e.Contains('@'))
                .Select(e => "@" + e.Split('@')[1])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (adminDomains.Count == 0)
            {
                throw new InvalidOperationException("Zero-Trust Security Violation: RootAdmins configuration must contain at least one valid administrator email address with a domain to seed the default tenant.");
            }

            defaultTenant = new TenantEntity
            {
                Id = IRootAdminService.RootTenantId,
                Slug = DefaultTenantSlug,
                Name = DefaultTenantName,
                InternalEmailDomains = string.Join(";", adminDomains),
                ExternalEmailDomains = "@client.com",
                IsActive = true,
                StartDate = DateTime.UtcNow.AddYears(-1),
                EndDate = DateTime.UtcNow.AddYears(20)
            };

            dbContext.Tenants.Add(defaultTenant);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Internal root anchor tenant provisioned successfully.");
        }

        // 2. Seed up to 3 Root Administrator accounts inside the default tenant
        var existingCount = await dbContext.TenantUsers
            .CountAsync(tu => tu.TenantId == defaultTenant.Id && !tu.IsDeleted, cancellationToken);

        var index = 1;
        var changesMade = false;

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
                    UserType = UserTypeEnum.CompanyUser,
                    IsActive = true,
                    StartDate = DateTime.UtcNow.AddYears(-1),
                    EndDate = DateTime.UtcNow.AddYears(20),
                    TimeZoneId = "UTC"
                };

                user.RowSignature = user.ComputeIntegritySignature(integrityKey);
                dbContext.Users.Add(user);

                var tenantUser = new TenantUserEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = defaultTenant.Id,
                    UserEntityId = user.Id,
                    Role = TenantRoleEnum.TenantAdmin,
                    IsActive = true,
                };
                tenantUser.RowSignature = tenantUser.ComputeIntegritySignature(integrityKey);
                dbContext.TenantUsers.Add(tenantUser);

                existingCount++;
                changesMade = true;
            }
            else
            {
                var hasMembership = await dbContext.TenantUsers
                    .AnyAsync(tu => tu.TenantId == defaultTenant.Id && tu.UserEntityId == existingUser.Id && !tu.IsDeleted, cancellationToken);

                if (!hasMembership)
                {
                    var tenantUser = new TenantUserEntity
                    {
                        Id = Guid.NewGuid(),
                        TenantId = defaultTenant.Id,
                        UserEntityId = existingUser.Id,
                        Role = TenantRoleEnum.TenantAdmin,
                        IsActive = true,
                    };
                    tenantUser.RowSignature = tenantUser.ComputeIntegritySignature(integrityKey);
                    dbContext.TenantUsers.Add(tenantUser);
                    changesMade = true;
                }
            }

            index++;
        }

        // 3. Self-healing signature synchronization strictly restricted to Development mode.
        // In Development, if the integrity secret is rotated or updated, automatically re-sign valid seed users.
        // Production environments remain strictly zero-trust and never automatically re-sign mismatched rows.
        if (isDevelopment)
        {
            var devUsers = await dbContext.Users
                .Where(u => !u.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var user in devUsers)
            {
                if (!user.VerifyIntegritySignature(integrityKey))
                {
                    user.RowSignature = user.ComputeIntegritySignature(integrityKey);
                    changesMade = true;
                    logger.LogInformation("Development self-healing: Synchronized HMAC integrity signature for '{Email}'.", user.Email);
                }
            }

            var devTenantUsers = await dbContext.TenantUsers
                .Where(u => !u.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var tu in devTenantUsers)
            {
                if (!tu.VerifyIntegritySignature(integrityKey))
                {
                    tu.RowSignature = tu.ComputeIntegritySignature(integrityKey);
                    changesMade = true;
                    logger.LogInformation("Development self-healing: Synchronized HMAC integrity signature for TenantUser '{Id}'.", tu.Id);
                }
            }

            var devProgRoles = await dbContext.ProgramUserRoles
                .Where(r => !r.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var pr in devProgRoles)
            {
                if (!pr.VerifyIntegritySignature(integrityKey))
                {
                    pr.RowSignature = pr.ComputeIntegritySignature(integrityKey);
                    changesMade = true;
                    logger.LogInformation("Development self-healing: Synchronized HMAC integrity signature for ProgramUserRole '{Id}'.", pr.Id);
                }
            }

            var devProjRoles = await dbContext.ProjectUserRoles
                .Where(r => !r.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var pjr in devProjRoles)
            {
                if (!pjr.VerifyIntegritySignature(integrityKey))
                {
                    pjr.RowSignature = pjr.ComputeIntegritySignature(integrityKey);
                    changesMade = true;
                    logger.LogInformation("Development self-healing: Synchronized HMAC integrity signature for ProjectUserRole '{Id}'.", pjr.Id);
                }
            }
        }

        if (changesMade)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Root super-administrators and integrity signatures synchronized successfully.");
        }
    }
}
