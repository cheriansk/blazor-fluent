using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services.RevisionProviders;

public sealed class ProgramCalendarRevisionRowProvider : IRevisionRowProvider
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ProgramCalendarRevisionRowProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public bool CanHandle(VersionedFeatureTypeEnum featureType) =>
        featureType == VersionedFeatureTypeEnum.ProgramCalendar;

    public async Task<List<RevisionRowComparableItem>> GetRowsAsync(
        Guid revisionHeaderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Calendars
            .AsNoTracking()
            .Where(c => c.RevisionHeaderId == revisionHeaderId && c.Level == TaskLevelEnum.Program && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RevisionRowComparableItem
        {
            RelId = r.RelId,
            Title = r.Title,
            EntryType = r.EntryType.ToString(),
            Status = r.Status.ToString(),
            StartDate = r.StartDate,
            EndDate = r.EndDate
        }).ToList();
    }
}

public sealed class ProjectCalendarRevisionRowProvider : IRevisionRowProvider
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ProjectCalendarRevisionRowProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public bool CanHandle(VersionedFeatureTypeEnum featureType) =>
        featureType == VersionedFeatureTypeEnum.ProjectCalendar;

    public async Task<List<RevisionRowComparableItem>> GetRowsAsync(
        Guid revisionHeaderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Calendars
            .AsNoTracking()
            .Where(c => c.RevisionHeaderId == revisionHeaderId && c.Level == TaskLevelEnum.Project && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RevisionRowComparableItem
        {
            RelId = r.RelId,
            Title = r.Title,
            EntryType = r.EntryType.ToString(),
            Status = r.Status.ToString(),
            StartDate = r.StartDate,
            EndDate = r.EndDate
        }).ToList();
    }
}
