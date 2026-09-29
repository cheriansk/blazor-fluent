using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class TenantService : ITenantService
{
    private const string TenantsCacheKey = "tenants:all";
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ITenantCacheService _cacheService;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        AppDbContext dbContext,
        IAuditService auditService,
        ITenantCacheService cacheService,
        ILogger<TenantService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _cacheService = cacheService;
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
        string slug,string code,
        string displayName,
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

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (await _dbContext.Tenants.AnyAsync(t => t.Slug == normalizedSlug, cancellationToken))
        {
            return Result<TenantEntity>.Failure($"A tenant with slug '{normalizedSlug}' already exists.");
        }

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Slug = normalizedSlug,
            Name = displayName.Trim(),
            IsActive = true,
            StartDate = startDate,
            EndDate = endDate
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict global tenant directory cache across all auto-scaled instances
        await _cacheService.RemoveGlobalAsync(TenantsCacheKey, cancellationToken);

        _logger.LogInformation("Provisioned new tenant: Slug={Slug}, Name={Name}, Code={Code}, StartDate={StartDate}, EndDate={EndDate}",
            tenant.Slug, tenant.Name,tenant.Code, tenant.StartDate, tenant.EndDate);

        await _auditService.LogUserActivityAsync(
            $"Created tenant '{tenant.Name}' (Slug: {tenant.Slug})",
            $"StartDate: {tenant.StartDate:yyyy-MM-dd}, EndDate: {tenant.EndDate:yyyy-MM-dd}",
            cancellationToken);

        return Result<TenantEntity>.Success(tenant);
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
