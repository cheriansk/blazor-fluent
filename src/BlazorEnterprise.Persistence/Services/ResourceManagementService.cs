using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Resources;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

public class ResourceManagementService : IResourceManagementService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IRevisionService _revisionService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ResourceManagementService> _logger;

    public ResourceManagementService(
        IDbContextFactory<AppDbContext> dbFactory,
        IRevisionService revisionService,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ILogger<ResourceManagementService> logger)
    {
        _dbFactory = dbFactory;
        _revisionService = revisionService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<List<ProjectResourceDto>> GetProjectResourcesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var activePlan = await _revisionService.GetActiveRevisionAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.ResourceAllocationPlan,
            projectId: projectId,
            cancellationToken: cancellationToken);

        var resources = await context.ProjectResources
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderBy(r => r.User!.FullName)
            .ToListAsync(cancellationToken);

        var rates = new Dictionary<Guid, (decimal Rate, string Currency)>();
        if (activePlan != null)
        {
            var rateEntities = await context.ResourceRates
                .AsNoTracking()
                .Where(r => r.RevisionHeaderId == activePlan.Id && !r.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var r in rateEntities)
            {
                rates[r.ProjectResourceId] = (r.HourlyRate, r.CurrencyCode);
            }
        }

        return resources.Select(r => new ProjectResourceDto
        {
            Id = r.Id,
            ProjectId = r.ProjectId ?? Guid.Empty,
            UserEntityId = r.UserEntityId,
            UserName = r.UserName,
            UserEmail = r.UserEmail,
            Role = r.Role,
            HourlyRate = rates.TryGetValue(r.Id, out var val) ? val.Rate : null,
            CurrencyCode = rates.TryGetValue(r.Id, out var v) ? v.Currency : "USD"
        }).ToList();
    }

    public async Task<List<UserEntity>> GetAvailableProjectUsersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var tenantId = _tenantContext.TenantId;

        var existingUserIds = await context.ProjectResources
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .Select(r => r.UserEntityId)
            .ToListAsync(cancellationToken);

        var tenantUsers = await context.TenantUsers
            .AsNoTracking()
            .Include(tu => tu.User)
            .Where(tu => tu.TenantId == tenantId && !tu.IsDeleted && tu.IsActive && !existingUserIds.Contains(tu.UserEntityId))
            .Select(tu => tu.User!)
            .Where(u => u != null)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        return tenantUsers;
    }

    public async Task<ProjectResourceEntity> AddResourceAsync(
        Guid projectId,
        Guid userEntityId,
        ProjectResourceRoleEnum role,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.ProjectResources
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.UserEntityId == userEntityId, cancellationToken);

        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.Role = role;
                context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
                return existing;
            }
            throw new InvalidOperationException("User is already registered as a resource on this project.");
        }

        var resource = new ProjectResourceEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantContext.TenantId,
            ProjectId = projectId,
            UserEntityId = userEntityId,
            Role = role
        };

        context.ProjectResources.Add(resource);
        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
        return resource;
    }

    public async Task RemoveResourceAsync(Guid projectResourceId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var resource = await context.ProjectResources.FindAsync([projectResourceId], cancellationToken);
        if (resource == null) return;

        resource.IsDeleted = true;
        resource.DeletedAtUtc = _dateTimeProvider.UtcNow;
        resource.DeletedBy = _currentUser.UserId;
        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
    }

    public async Task UpdateResourceRoleAsync(Guid projectResourceId, ProjectResourceRoleEnum newRole, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var resource = await context.ProjectResources.FindAsync([projectResourceId], cancellationToken);
        if (resource == null) return;

        resource.Role = newRole;
        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
    }

    public async Task<RevisionHeaderEntity> GetOrCreateDraftAllocationPlanAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var revisions = await _revisionService.GetRevisionsAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.ResourceAllocationPlan,
            projectId: projectId,
            cancellationToken: cancellationToken);

        var existingDraft = revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Draft);
        if (existingDraft != null)
        {
            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var header = await context.RevisionHeaders.FindAsync([existingDraft.Id], cancellationToken);
            if (header != null) return header;
        }

        var active = revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Active);
        var draft = await _revisionService.CreateDraftAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.ResourceAllocationPlan,
            $"Allocation Plan v{revisions.Count + 1}",
            sourceRevisionHeaderId: active?.Id,
            projectId: projectId,
            cancellationToken: cancellationToken);

        return draft;
    }

    public async Task<ResourcePlanSummaryDto> GetResourcePlanSummaryAsync(
        Guid projectId,
        Guid? revisionHeaderId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project == null) throw new InvalidOperationException($"Project {projectId} not found.");

        RevisionHeaderEntity? header = null;
        if (revisionHeaderId.HasValue)
        {
            header = await context.RevisionHeaders.FindAsync([revisionHeaderId.Value], cancellationToken);
        }

        if (header == null)
        {
            header = await _revisionService.GetActiveRevisionAsync(
                TaskLevelEnum.Project,
                VersionedFeatureTypeEnum.ResourceAllocationPlan,
                projectId: projectId,
                cancellationToken: cancellationToken);
        }

        if (header == null)
        {
            header = await GetOrCreateDraftAllocationPlanAsync(projectId, cancellationToken);
        }

        var startDate = DateOnly.FromDateTime(project.StartDate);
        var endDate = DateOnly.FromDateTime(project.DueDate);
        if (endDate < startDate) endDate = startDate.AddDays(14);

        var dates = new List<DateOnly>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            dates.Add(d);
        }

        var resources = await context.ProjectResources
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderBy(r => r.User!.FullName)
            .ToListAsync(cancellationToken);

        var allocations = await context.ResourceAllocations
            .AsNoTracking()
            .Where(a => a.RevisionHeaderId == header.Id && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        var rates = await context.ResourceRates
            .AsNoTracking()
            .Where(r => r.RevisionHeaderId == header.Id && !r.IsDeleted)
            .ToDictionaryAsync(r => r.ProjectResourceId, r => r.HourlyRate, cancellationToken);

        var resourceSummaries = new List<ResourceDailyAllocationDto>();
        var dailyTotals = dates.ToDictionary(d => d, _ => 0);
        decimal totalPlanCost = 0;

        foreach (var res in resources)
        {
            var resAlloc = allocations.Where(a => a.ProjectResourceId == res.Id).ToDictionary(a => DateOnly.FromDateTime(a.AllocationDate), a => a.AllocationPercentage);
            var allocMap = new Dictionary<DateOnly, int>();
            double totalHours = 0;

            foreach (var d in dates)
            {
                var pct = resAlloc.TryGetValue(d, out var val) ? val : 0;
                allocMap[d] = pct;
                dailyTotals[d] += pct;
                totalHours += (pct / 100.0) * 8.0;
            }

            var rate = rates.TryGetValue(res.Id, out var rVal) ? rVal : 0m;
            var resCost = (decimal)totalHours * rate;
            totalPlanCost += resCost;

            resourceSummaries.Add(new ResourceDailyAllocationDto
            {
                ProjectResourceId = res.Id,
                ResourceName = res.UserName,
                Role = res.Role,
                HourlyRate = rate,
                Allocations = allocMap,
                TotalAllocatedHours = totalHours,
                TotalCost = resCost
            });
        }

        return new ResourcePlanSummaryDto
        {
            RevisionHeaderId = header.Id,
            VerNum = header.VerNum,
            Status = header.Status,
            StartDate = startDate,
            EndDate = endDate,
            Dates = dates,
            Resources = resourceSummaries,
            DailyTotals = dailyTotals,
            TotalPlanCost = totalPlanCost
        };
    }

    public async Task SetResourceAllocationAsync(
        Guid revisionHeaderId,
        Guid projectResourceId,
        DateOnly date,
        int allocationPercentage,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var header = await context.RevisionHeaders.FindAsync([revisionHeaderId], cancellationToken);
        if (header == null) throw new InvalidOperationException("Allocation plan not found.");
        if (header.Status != RevisionStatusEnum.Draft)
            throw new InvalidOperationException("Cannot modify finalized or locked plan.");

        var resource = await context.ProjectResources.FindAsync([projectResourceId], cancellationToken);
        if (resource == null) throw new InvalidOperationException("Resource not found.");

        var allocDateTime = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var existing = await context.ResourceAllocations
            .FirstOrDefaultAsync(a => a.RevisionHeaderId == revisionHeaderId &&
                                      a.ProjectResourceId == projectResourceId &&
                                      a.AllocationDate == allocDateTime, cancellationToken);

        if (existing != null)
        {
            existing.AllocationPercentage = allocationPercentage;
            existing.IsDeleted = false;
        }
        else
        {
            var alloc = new ResourceAllocationEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = _tenantContext.TenantId,
                ProjectId = resource.ProjectId,
                ProjectResourceId = projectResourceId,
                RevisionHeaderId = revisionHeaderId,
                RelId = Guid.CreateVersion7(),
                VerNum = header.VerNum,
                AllocationDate = allocDateTime,
                AllocationPercentage = allocationPercentage
            };
            context.ResourceAllocations.Add(alloc);
        }

        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
    }

    public async Task BulkSetAllocationAsync(
        Guid revisionHeaderId,
        Guid projectResourceId,
        DateOnly startDate,
        DateOnly endDate,
        int allocationPercentage,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var header = await context.RevisionHeaders.FindAsync([revisionHeaderId], cancellationToken);
        if (header == null) throw new InvalidOperationException("Allocation plan not found.");
        if (header.Status != RevisionStatusEnum.Draft)
            throw new InvalidOperationException("Cannot modify finalized or locked plan.");

        var resource = await context.ProjectResources.FindAsync([projectResourceId], cancellationToken);
        if (resource == null) throw new InvalidOperationException("Resource not found.");

        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var existingAllocations = await context.ResourceAllocations
            .Where(a => a.RevisionHeaderId == revisionHeaderId &&
                        a.ProjectResourceId == projectResourceId &&
                        a.AllocationDate >= startDateTime &&
                        a.AllocationDate <= endDateTime)
            .ToListAsync(cancellationToken);

        var allocationsByDate = existingAllocations.ToDictionary(a => a.AllocationDate, a => a);

        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            var allocDateTime = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            if (allocationsByDate.TryGetValue(allocDateTime, out var existing))
            {
                existing.AllocationPercentage = allocationPercentage;
                existing.IsDeleted = false;
            }
            else
            {
                var alloc = new ResourceAllocationEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = _tenantContext.TenantId,
                    ProjectId = resource.ProjectId,
                    ProjectResourceId = projectResourceId,
                    RevisionHeaderId = revisionHeaderId,
                    RelId = Guid.CreateVersion7(),
                    VerNum = header.VerNum,
                    AllocationDate = allocDateTime,
                    AllocationPercentage = allocationPercentage
                };
                context.ResourceAllocations.Add(alloc);
            }
        }

        context.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            context.IsTransactionScopeCommitAuthorized = false;
        }
    }

    public async Task CopyPreviousDayAllocationAsync(
        Guid revisionHeaderId,
        Guid projectResourceId,
        DateOnly targetDate,
        CancellationToken cancellationToken = default)
    {
        var prevDate = targetDate.AddDays(-1);
        var prevDateTime = prevDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var prev = await context.ResourceAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.RevisionHeaderId == revisionHeaderId &&
                                      a.ProjectResourceId == projectResourceId &&
                                      a.AllocationDate == prevDateTime && !a.IsDeleted, cancellationToken);

        var pct = prev?.AllocationPercentage ?? 0;
        await SetResourceAllocationAsync(revisionHeaderId, projectResourceId, targetDate, pct, cancellationToken);
    }

    public async Task<List<ProjectResourceDto>> GetRateCardAsync(
        Guid projectId,
        Guid? revisionHeaderId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        Guid targetPlanId;
        if (revisionHeaderId.HasValue)
        {
            targetPlanId = revisionHeaderId.Value;
        }
        else
        {
            var active = await _revisionService.GetActiveRevisionAsync(
                TaskLevelEnum.Project,
                VersionedFeatureTypeEnum.ResourceAllocationPlan,
                projectId: projectId,
                cancellationToken: cancellationToken);

            targetPlanId = active?.Id ?? (await GetOrCreateDraftAllocationPlanAsync(projectId, cancellationToken)).Id;
        }

        var resources = await context.ProjectResources
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderBy(r => r.User!.FullName)
            .ToListAsync(cancellationToken);

        var rates = await context.ResourceRates
            .AsNoTracking()
            .Where(r => r.RevisionHeaderId == targetPlanId && !r.IsDeleted)
            .ToDictionaryAsync(r => r.ProjectResourceId, cancellationToken);

        return resources.Select(r => new ProjectResourceDto
        {
            Id = r.Id,
            ProjectId = r.ProjectId ?? Guid.Empty,
            UserEntityId = r.UserEntityId,
            UserName = r.UserName,
            UserEmail = r.UserEmail,
            Role = r.Role,
            HourlyRate = rates.TryGetValue(r.Id, out var val) ? val.HourlyRate : 0m,
            CurrencyCode = rates.TryGetValue(r.Id, out var v) ? v.CurrencyCode : "USD"
        }).ToList();
    }

    public async Task SetResourceRateAsync(
        Guid revisionHeaderId,
        Guid projectResourceId,
        decimal hourlyRate,
        string currency = "USD",
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var header = await context.RevisionHeaders.FindAsync([revisionHeaderId], cancellationToken);
        if (header == null) throw new InvalidOperationException("Allocation plan not found.");
        if (header.Status != RevisionStatusEnum.Draft)
            throw new InvalidOperationException("Rates can only be updated on a draft plan.");

        var resource = await context.ProjectResources.FindAsync([projectResourceId], cancellationToken);
        if (resource == null) throw new InvalidOperationException("Resource not found.");

        var existing = await context.ResourceRates
            .FirstOrDefaultAsync(r => r.RevisionHeaderId == revisionHeaderId &&
                                      r.ProjectResourceId == projectResourceId, cancellationToken);

        if (existing != null)
        {
            existing.HourlyRate = hourlyRate;
            existing.CurrencyCode = currency;
            existing.IsDeleted = false;
        }
        else
        {
            var rate = new ResourceRateEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = _tenantContext.TenantId,
                ProjectId = resource.ProjectId,
                ProjectResourceId = projectResourceId,
                RevisionHeaderId = revisionHeaderId,
                RelId = Guid.CreateVersion7(),
                VerNum = header.VerNum,
                HourlyRate = hourlyRate,
                CurrencyCode = currency
            };
            context.ResourceRates.Add(rate);
        }

        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
    }

    public async Task BulkSetRateByRoleAsync(
        Guid revisionHeaderId,
        Guid projectId,
        ProjectResourceRoleEnum role,
        decimal hourlyRate,
        string currency = "USD",
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var header = await context.RevisionHeaders.FindAsync([revisionHeaderId], cancellationToken);
        if (header == null) throw new InvalidOperationException("Allocation plan not found.");
        if (header.Status != RevisionStatusEnum.Draft)
            throw new InvalidOperationException("Cannot modify finalized or locked plan.");

        var resources = await context.ProjectResources
            .Where(r => r.ProjectId == projectId && r.Role == role && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        if (resources.Count == 0) return;

        var resourceIds = resources.Select(r => r.Id).ToList();
        var existingRates = await context.ResourceRates
            .Where(r => r.RevisionHeaderId == revisionHeaderId && resourceIds.Contains(r.ProjectResourceId))
            .ToListAsync(cancellationToken);

        var ratesByResource = existingRates.ToDictionary(r => r.ProjectResourceId, r => r);

        foreach (var res in resources)
        {
            if (ratesByResource.TryGetValue(res.Id, out var existing))
            {
                existing.HourlyRate = hourlyRate;
                existing.CurrencyCode = currency;
                existing.IsDeleted = false;
            }
            else
            {
                var rate = new ResourceRateEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = _tenantContext.TenantId,
                    ProjectId = projectId,
                    ProjectResourceId = res.Id,
                    RevisionHeaderId = revisionHeaderId,
                    RelId = Guid.CreateVersion7(),
                    VerNum = header.VerNum,
                    HourlyRate = hourlyRate,
                    CurrencyCode = currency
                };
                context.ResourceRates.Add(rate);
            }
        }

        context.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            context.IsTransactionScopeCommitAuthorized = false;
        }
    }

    public async Task<List<RevisionSummaryDto>> GetAllocationPlanHistoryAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _revisionService.GetRevisionsAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.ResourceAllocationPlan,
            projectId: projectId,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> IsAllocationPlanLockedAsync(Guid revisionHeaderId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var header = await context.RevisionHeaders.FindAsync([revisionHeaderId], cancellationToken);
        return header == null || header.Status != RevisionStatusEnum.Draft;
    }
}
