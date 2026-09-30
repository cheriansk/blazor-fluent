using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class TenantService : ITenantService
{
    private const string TenantsCacheKey = "tenants:all";
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ITenantCacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        AppDbContext dbContext,
        IAuditService auditService,
        ITenantCacheService cacheService,
        IConfiguration configuration,
        ILogger<TenantService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _cacheService = cacheService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantEntity>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        return await _cacheService.GetGlobalOrCreateAsync(
            TenantsCacheKey,
            async token =>
            {
                return (IReadOnlyList<TenantEntity>)await _dbContext.Tenants
                    .AsNoTracking()
                    .OrderBy(t => t.Name)
                    .ToListAsync(token);
            },
            expiration: TimeSpan.FromMinutes(60),
            cancellationToken: cancellationToken);
    }

    public async Task<Result<TenantEntity>> CreateTenantAsync(
        string slug,
        string code,
        string displayName,
        IEnumerable<string> internalEmailDomains,
        IEnumerable<string> externalEmailDomains,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Result<TenantEntity>.Failure("Slug is required.");

        if (string.IsNullOrWhiteSpace(code))
            return Result<TenantEntity>.Failure("Code is required.");

        if (string.IsNullOrWhiteSpace(displayName))
            return Result<TenantEntity>.Failure("DisplayName is required.");

        if (endDate < startDate)
            return Result<TenantEntity>.Failure("End date cannot precede start date.");

        var normalizedInternal = (internalEmailDomains ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d =>
            {
                var trimmed = d.Trim().ToLowerInvariant();
                return trimmed.StartsWith('@') ? trimmed : "@" + trimmed;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedExternal = (externalEmailDomains ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d =>
            {
                var trimmed = d.Trim().ToLowerInvariant();
                return trimmed.StartsWith('@') ? trimmed : "@" + trimmed;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedInternal.Count is < 1 or > 5)
            return Result<TenantEntity>.Failure("Between 1 and 5 Internal email domains must be configured.");

        if (normalizedExternal.Count is < 1 or > 5)
            return Result<TenantEntity>.Failure("Between 1 and 5 External email domains must be configured.");

        var overlap = normalizedInternal.Intersect(normalizedExternal, StringComparer.OrdinalIgnoreCase).ToList();
        if (overlap.Any())
            return Result<TenantEntity>.Failure($"Internal and External email domains cannot overlap. Overlapping domain(s): {string.Join(", ", overlap)}.");

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (await _dbContext.Tenants.AnyAsync(t => t.Slug == normalizedSlug, cancellationToken))
        {
            return Result<TenantEntity>.Failure($"A tenant with slug '{normalizedSlug}' already exists.");
        }

        var internalJoined = string.Join(";", normalizedInternal);
        var externalJoined = string.Join(";", normalizedExternal);

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Slug = normalizedSlug,
            Name = displayName.Trim(),
            InternalEmailDomains = internalJoined,
            ExternalEmailDomains = externalJoined,
            IsActive = true,
            StartDate = startDate,
            EndDate = endDate
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict global tenant directory cache across all auto-scaled instances
        await _cacheService.RemoveGlobalAsync(TenantsCacheKey, cancellationToken);

        _logger.LogInformation("Provisioned new tenant: Slug={Slug}, Name={Name}, Code={Code}, InternalDomains={InternalDomains}, ExternalDomains={ExternalDomains}, StartDate={StartDate}, EndDate={EndDate}",
            tenant.Slug, tenant.Name, tenant.Code, tenant.InternalEmailDomains, tenant.ExternalEmailDomains, tenant.StartDate, tenant.EndDate);

        await _auditService.LogUserActivityAsync(
            $"Created tenant '{tenant.Name}' (Slug: {tenant.Slug})",
            $"Code: {tenant.Code}, Internal: {tenant.InternalEmailDomains}, External: {tenant.ExternalEmailDomains}, StartDate: {tenant.StartDate:yyyy-MM-dd}, EndDate: {tenant.EndDate:yyyy-MM-dd}",
            cancellationToken);

        return Result<TenantEntity>.Success(tenant);
    }

    public Task<Result<TenantEntity>> CreateTenantAsync(
        string slug,
        string code,
        string displayName,
        string internalEmailDomain,
        string externalEmailDomain,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return CreateTenantAsync(slug, code, displayName, [internalEmailDomain], [externalEmailDomain], startDate, endDate, cancellationToken);
    }

    public async Task<IReadOnlyList<UserEntity>> GetTenantUsersAsync(string tenantSlug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.DefaultTenantId == normalizedSlug && !u.IsDeleted)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<UserEntity>> AddTenantUserAsync(
        string tenantSlug,
        string fullName,
        string email,
        DateTime startDate,
        DateTime endDate,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result<UserEntity>.Failure("Full Name is required.");

        if (string.IsNullOrWhiteSpace(email))
            return Result<UserEntity>.Failure("Email address is required.");

        if (endDate < startDate)
            return Result<UserEntity>.Failure("End date cannot precede start date.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken);

        if (tenant is null)
            return Result<UserEntity>.Failure($"Tenant '{tenantSlug}' was not found.");

        // Automatically determine UserType from configured tenant domains
        UserType userType;
        var internalDomains = tenant.GetInternalDomains();
        var externalDomains = tenant.GetExternalDomains();

        if (internalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.CompanyUser;
        }
        else if (externalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.ClientUser;
        }
        else
        {
            return Result<UserEntity>.Failure(
                $"Email '{normalizedEmail}' does not match any configured Internal domain ({string.Join(", ", internalDomains)}) or External domain ({string.Join(", ", externalDomains)}) for tenant '{tenant.Name}'.");
        }

        // Check if user already exists with this email
        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken))
        {
            return Result<UserEntity>.Failure($"A user with email '{normalizedEmail}' already exists.");
        }

        var secretKey = _configuration["Security:IntegritySecret"] ?? "BlazorFluent-Secret-Key-Change-In-Production-2026";
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = fullName.Trim(),
            UserType = userType,
            DefaultTenantId = normalizedSlug,
            IsActive = isActive,
            StartDateUtc = startDate.ToUniversalTime(),
            EndDateUtc = endDate.ToUniversalTime()
        };

        user.RowSignature = user.ComputeIntegritySignature(secretKey);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added user '{Email}' ({UserType}) to tenant '{TenantSlug}'", user.Email, user.UserType, tenantSlug);

        await _auditService.LogUserActivityAsync(
            $"Created tenant user '{user.FullName}' ({user.Email})",
            $"Tenant: {tenantSlug}, Type: {user.UserType}, Active: {user.IsActive}",
            cancellationToken);

        return Result<UserEntity>.Success(user);
    }

    public async Task<Result<UserEntity>> UpdateTenantUserAsync(
        string tenantSlug,
        Guid userId,
        string fullName,
        string email,
        DateTime startDate,
        DateTime endDate,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result<UserEntity>.Failure("Full Name is required.");

        if (string.IsNullOrWhiteSpace(email))
            return Result<UserEntity>.Failure("Email address is required.");

        if (endDate < startDate)
            return Result<UserEntity>.Failure("End date cannot precede start date.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken);

        if (tenant is null)
            return Result<UserEntity>.Failure($"Tenant '{tenantSlug}' was not found.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.DefaultTenantId == normalizedSlug && !u.IsDeleted, cancellationToken);

        if (user is null)
            return Result<UserEntity>.Failure($"User was not found in tenant '{tenantSlug}'.");

        var internalDomains = tenant.GetInternalDomains();
        var externalDomains = tenant.GetExternalDomains();

        UserType userType;
        if (internalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.CompanyUser;
        }
        else if (externalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.ClientUser;
        }
        else
        {
            return Result<UserEntity>.Failure(
                $"Email '{normalizedEmail}' does not match any configured Internal domain ({string.Join(", ", internalDomains)}) or External domain ({string.Join(", ", externalDomains)}) for tenant '{tenant.Name}'.");
        }

        // If email changed, check uniqueness
        if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != userId && !u.IsDeleted, cancellationToken))
            {
                return Result<UserEntity>.Failure($"A user with email '{normalizedEmail}' already exists.");
            }
        }

        user.FullName = fullName.Trim();
        user.Email = normalizedEmail;
        user.UserType = userType;
        user.StartDateUtc = startDate.ToUniversalTime();
        user.EndDateUtc = endDate.ToUniversalTime();
        user.IsActive = isActive;

        var secretKey = _configuration["Security:IntegritySecret"] ?? "BlazorFluent-Secret-Key-Change-In-Production-2026";
        user.RowSignature = user.ComputeIntegritySignature(secretKey);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated user '{Email}' ({UserType}, Active={Active}) in tenant '{TenantSlug}'", user.Email, user.UserType, user.IsActive, tenantSlug);

        await _auditService.LogUserActivityAsync(
            $"Updated tenant user '{user.FullName}' ({user.Email})",
            $"Tenant: {tenantSlug}, Type: {user.UserType}, Active: {user.IsActive}, StartDate: {user.StartDateUtc:yyyy-MM-dd}, EndDate: {user.EndDateUtc:yyyy-MM-dd}",
            cancellationToken);

        return Result<UserEntity>.Success(user);
    }

    public async Task<Result> UpdateTenantStatusAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants.FindAsync([tenantId], cancellationToken);
        if (tenant is null) return Result.Failure("Tenant not found.");

        tenant.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict global tenant directory cache across all auto-scaled instances
        await _cacheService.RemoveGlobalAsync(TenantsCacheKey, cancellationToken);

        // If deactivated, purge all tenant session/circuit cache entries across all nodes
        if (!isActive)
        {
            await _cacheService.InvalidateTenantAsync(tenant.Slug, cancellationToken);
            await _cacheService.InvalidateTenantAsync(tenant.Id.ToString(), cancellationToken);
        }

        _logger.LogInformation("Updated tenant status: Slug={Slug}, IsActive={IsActive}", tenant.Slug, isActive);

        await _auditService.LogSecurityEventAsync(
            $"Tenant status changed for '{tenant.Name}' to {(isActive ? "Active" : "Inactive")}",
            isActive ? AuditSeverity.Information : AuditSeverity.Warning,
            $"TenantId: {tenant.Id}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UpdateTenantDatesAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
            return Result.Failure("End date cannot precede start date.");

        var tenant = await _dbContext.Tenants.FindAsync([tenantId], cancellationToken);
        if (tenant is null) return Result.Failure("Tenant not found.");

        tenant.StartDate = startDate;
        tenant.EndDate = endDate;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict global tenant directory cache across all auto-scaled instances
        await _cacheService.RemoveGlobalAsync(TenantsCacheKey, cancellationToken);

        _logger.LogInformation("Updated tenant dates: Slug={Slug}, StartDate={StartDate}, EndDate={EndDate}",
            tenant.Slug, startDate, endDate);

        await _auditService.LogUserActivityAsync(
            $"Updated contract dates for tenant '{tenant.Name}'",
            $"StartDate: {startDate:yyyy-MM-dd}, EndDate: {endDate:yyyy-MM-dd}",
            cancellationToken);

        return Result.Success();
    }
}
