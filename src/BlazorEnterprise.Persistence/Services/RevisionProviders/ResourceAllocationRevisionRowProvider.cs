using BlazorEnterprise.Core.Domain.Resources;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services.RevisionProviders;

public sealed class ResourceAllocationRevisionRowProvider : IRevisionRowProvider
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ResourceAllocationRevisionRowProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public bool CanHandle(VersionedFeatureTypeEnum featureType) =>
        featureType == VersionedFeatureTypeEnum.ResourceAllocationPlan;

    public async Task<List<RevisionRowComparableItem>> GetRowsAsync(
        Guid revisionHeaderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ResourceAllocations
            .AsNoTracking()
            .Include(r => r.ProjectResource)
                .ThenInclude(pr => pr!.User)
            .Where(r => r.RevisionHeaderId == revisionHeaderId && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RevisionRowComparableItem
        {
            RelId = r.RelId,
            Title = r.ProjectResource?.UserName ?? r.ProjectResourceId.ToString(),
            EntryType = r.ProjectResource?.Role.ToString() ?? "Resource",
            Status = $"{r.AllocationPercentage}%",
            StartDate = r.AllocationDate,
            EndDate = r.AllocationDate
        }).ToList();
    }
}
