using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorEnterprise.Persistence.Initialization;

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

        var rootAdminService = scope.ServiceProvider.GetRequiredService<IRootAdminService>();
        var adminEmails = rootAdminService.GetRootAdminEmails();
        if (adminEmails.Count == 0) throw new InvalidOperationException("Zero-Trust Security Violation: No root administrator emails configured for development seeding.");
        var primaryAdminEmail = adminEmails[0].Trim().ToLowerInvariant();

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(rootAdminService.RootTenantId, rootAdminService.RootTenantSlug, rootAdminService.RootTenantName);

        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        currentUser?.RestoreUserContext(primaryAdminEmail, primaryAdminEmail, "Development Administrator", isRootAdmin: true);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var changesMade = false;

        // 1. Optional Custom Tenant Seeding from DevelopmentSeedOptions
        if (seedOptions != null && seedOptions.Enabled && !string.IsNullOrWhiteSpace(seedOptions.TenantSlug))
        {
            var tenantSlug = seedOptions.TenantSlug.Trim();
            var existingTenant = await dbContext.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == tenantSlug, cancellationToken);
            TenantEntity? createdTenant = null;

            if (existingTenant is null)
            {
                logger.LogInformation("DevelopmentSeeder: Provisioning configured dev tenant '{Slug}'...", tenantSlug);
                createdTenant = new TenantEntity
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
                dbContext.Tenants.Add(createdTenant);
                changesMade = true;
            }

            // Optional configured admin user
            if (!string.IsNullOrWhiteSpace(seedOptions.AdminEmail))
            {
                var adminEmail = seedOptions.AdminEmail.Trim().ToLowerInvariant();
                var existingAdmin = dbContext.Users.Local.FirstOrDefault(u => string.Equals(u.Email, adminEmail, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted)
                    ?? await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == adminEmail && !u.IsDeleted, cancellationToken);

                if (existingAdmin is null)
                {
                    logger.LogInformation("DevelopmentSeeder: Provisioning configured dev admin '{Email}'...", adminEmail);
                    var adminUser = new UserEntity
                    {
                        Id = Guid.NewGuid(),
                        Email = adminEmail,
                        FullName = string.IsNullOrWhiteSpace(seedOptions.AdminFullName) ? "Configured Dev Admin" : seedOptions.AdminFullName,
                        UserType = UserTypeEnum.CompanyUser,
                        UserKind = UserKindEnum.Human,
                        IsActive = true,
                        StartDate = DateTime.UtcNow.AddDays(-30),
                        EndDate = DateTime.UtcNow.AddYears(1),
                        TimeZoneId = "UTC"
                    };
                    adminUser.RowSignature = adminUser.ComputeIntegritySignature(integrityKey);
                    dbContext.Users.Add(adminUser);

                    var assignedTenantId = existingTenant?.Id ?? createdTenant?.Id;
                    if (assignedTenantId.HasValue && !await dbContext.TenantUsers.IgnoreQueryFilters().AnyAsync(tu => tu.TenantId == assignedTenantId.Value && tu.UserEntityId == adminUser.Id && !tu.IsDeleted, cancellationToken))
                    {
                        var tenantUser = new TenantUserEntity
                        {
                            Id = Guid.NewGuid(),
                            TenantId = assignedTenantId.Value,
                            UserEntityId = adminUser.Id,
                            Role = TenantRoleEnum.TenantAdmin,
                            IsActive = true,
                        };
                        tenantUser.RowSignature = tenantUser.ComputeIntegritySignature(integrityKey);
                        dbContext.TenantUsers.Add(tenantUser);
                    }

                    changesMade = true;
                }
            }

            // Commit custom tenant and admin before proceeding to personas to prevent unique constraint races
            if (changesMade)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                changesMade = false;
            }
        }

        // 2. DevLogin users seeding from DevLoginOptions
        if (devLoginOptions != null && devLoginOptions.Enabled && devLoginOptions.Users.Count > 0)
        {
            var targetTenantSlug = seedOptions?.TenantSlug ?? rootAdminService.RootTenantSlug;
            var targetTenant = await dbContext.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == targetTenantSlug, cancellationToken);

            // Separation of duties: App Admin only assigns TenantAdmin.
            // Switch execution context to targetTenant and its TenantAdmin so that TenantMembers are added within the tenant by its TenantAdmin.
            if (targetTenant is not null && targetTenant.Id != rootAdminService.RootTenantId)
            {
                var tenantAdminEmail = seedOptions?.AdminEmail?.Trim().ToLowerInvariant();
                var tenantAdminUser = !string.IsNullOrWhiteSpace(tenantAdminEmail)
                    ? (dbContext.Users.Local.FirstOrDefault(u => string.Equals(u.Email, tenantAdminEmail, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted)
                       ?? await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == tenantAdminEmail && !u.IsDeleted, cancellationToken))
                    : null;

                if (tenantAdminUser is not null)
                {
                    tenantContext.SetTenant(targetTenant.Id, targetTenant.Slug, targetTenant.Name);
                    currentUser?.RestoreUserContext(tenantAdminUser.Id.ToString(), tenantAdminUser.Email, tenantAdminUser.FullName, isRootAdmin: false);
                }
            }

            foreach (var devUser in devLoginOptions.Users)
            {
                if (string.IsNullOrWhiteSpace(devUser.Email)) continue;

                var email = devUser.Email.Trim().ToLowerInvariant();
                var existingUser = dbContext.Users.Local.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted)
                    ?? await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);

                Guid personaUserId;

                if (existingUser is null)
                {
                    logger.LogInformation("DevelopmentSeeder: Provisioning configured dev login persona '{Email}' ({Label})...", email, devUser.Label);
                    var user = new UserEntity
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        FullName = string.IsNullOrWhiteSpace(devUser.Label) ? email : devUser.Label,
                        UserType = UserTypeEnum.CompanyUser,
                        UserKind = UserKindEnum.Human,
                        IsActive = true,
                        StartDate = DateTime.UtcNow.AddDays(-30),
                        EndDate = DateTime.UtcNow.AddYears(1),
                        TimeZoneId = "UTC"
                    };
                    user.RowSignature = user.ComputeIntegritySignature(integrityKey);
                    dbContext.Users.Add(user);
                    personaUserId = user.Id;
                    changesMade = true;
                }
                else
                {
                    personaUserId = existingUser.Id;
                }

                if (targetTenant is not null)
                {
                    var hasMembership = dbContext.TenantUsers.Local.Any(tu => tu.TenantId == targetTenant.Id && tu.UserEntityId == personaUserId && !tu.IsDeleted)
                        || await dbContext.TenantUsers.IgnoreQueryFilters().AnyAsync(tu => tu.TenantId == targetTenant.Id && tu.UserEntityId == personaUserId && !tu.IsDeleted, cancellationToken);

                    if (!hasMembership)
                    {
                        var tenantUser = new TenantUserEntity
                        {
                            Id = Guid.NewGuid(),
                            TenantId = targetTenant.Id,
                            UserEntityId = personaUserId,
                            Role = TenantRoleEnum.TenantMember,
                            IsActive = true,
                        };
                        tenantUser.RowSignature = tenantUser.ComputeIntegritySignature(integrityKey);
                        dbContext.TenantUsers.Add(tenantUser);
                        changesMade = true;
                    }
                }
            }
        }

        if (changesMade)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("DevelopmentSeeder: Completed successfully.");
        }

        // Restore ambient context back to Root Administrator in Root Tenant
        tenantContext.SetTenant(rootAdminService.RootTenantId, rootAdminService.RootTenantSlug, rootAdminService.RootTenantName);
        currentUser?.RestoreUserContext(primaryAdminEmail, primaryAdminEmail, "Development Administrator", isRootAdmin: true);
    }
}
