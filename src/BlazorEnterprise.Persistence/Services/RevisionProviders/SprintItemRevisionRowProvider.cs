using BlazorEnterprise.Core.Domain.Sprints;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services.RevisionProviders;

public sealed class SprintItemRevisionRowProvider : IRevisionRowProvider
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SprintItemRevisionRowProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public bool CanHandle(VersionedFeatureTypeEnum featureType) =>
        featureType == VersionedFeatureTypeEnum.SprintPlan;

    public async Task<List<RevisionRowComparableItem>> GetRowsAsync(
        Guid revisionHeaderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.SprintItemVersions
            .AsNoTracking()
            .Include(iv => iv.Item)
            .Include(iv => iv.Sprint)
            .Where(iv => iv.RevisionHeaderId == revisionHeaderId && !iv.IsDeleted)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RevisionRowComparableItem
        {
            RelId = r.RelId,
            Title = r.Item?.Title ?? r.ItemId.ToString(),
            EntryType = r.Sprint?.SprintName ?? "Backlog",
            Status = r.Status.ToString(),
            StartDate = r.EffectiveFromDate,
            EndDate = r.EffectiveToDate
        }).ToList();
    }
}
