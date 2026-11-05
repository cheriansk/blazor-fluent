using BlazorEnterprise.Persistence.Extensions;
using BlazorEnterprise.Core.Domain.Versioning;

using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

public class CalendarService : ICalendarService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IEnumerable<IWorkItemRollupProvider> _rollupProviders;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        IDbContextFactory<AppDbContext> dbFactory,
        IEnumerable<IWorkItemRollupProvider> rollupProviders,
        ICurrentUser currentUser,
        ILogger<CalendarService> logger)
    {
        _dbFactory = dbFactory;
        _rollupProviders = rollupProviders;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetProgramCalendarAsync(
        Guid tenantId,
        Guid programId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        // 1. Direct program calendar entries
        var query = _context.ProgramCalendars
            .AsNoTracking()
            .Where(c => c.ProgramId == programId && !c.IsDeleted &&
                        c.RevisionHeader!.Status == RevisionStatusEnum.Active &&
                        c.StartDate <= endUtc && (c.EndDate ?? c.StartDate) >= startUtc);

        if (tenantId != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == tenantId);
        }

        var directEntries = await query.ToListAsync(ct);

        var directDtos = directEntries.Select(c => new CalendarItemDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            StartUtc = c.StartDate,
            EndUtc = c.EndDate ?? c.StartDate,
            IsAllDay = true,
            ItemCategory = c.EntryType.ToString(),
            PriorityOrCriticality = "Normal",
            Status = c.Status.ToString(),
            OwnerOrAssignee = c.OwnerEmails ?? string.Empty,
            OwnerEmails = c.OwnerEmails,
            Comments = c.Comments,
            ParentTimelineId = c.ParentTimelineId,
            ColorHex = "#0078d4",
            BadgeIcon = "Calendar",
            ScopeId = c.ProgramId,
            Level = TaskLevelEnum.Program
        }).ToList();

        // 2. Extensible plugin fan-out for work items
        // Sequential execution across shared AppDbContext to prevent concurrency violations
        var combinedRollups = new List<CalendarItemDto>();
        foreach (var provider in _rollupProviders)
        {
            var rollupItems = await provider.GetCalendarRollupItemsAsync(
                tenantId, TaskLevelEnum.Program, programId, null, startUtc, endUtc, ct);
            combinedRollups.AddRange(rollupItems);
        }

        return directDtos.Concat(combinedRollups)
            .OrderBy(i => i.StartUtc)
            .ThenBy(i => i.EndUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetProjectCalendarAsync(
        Guid tenantId,
        Guid projectId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        // 1. Direct project calendar entries
        var query = _context.ProjectCalendars
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId && !c.IsDeleted &&
                        c.RevisionHeader!.Status == RevisionStatusEnum.Active &&
                        c.StartDate <= endUtc && (c.EndDate ?? c.StartDate) >= startUtc);

        if (tenantId != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == tenantId);
        }

        var directEntries = await query.ToListAsync(ct);

        var directDtos = directEntries.Select(c => new CalendarItemDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            StartUtc = c.StartDate,
            EndUtc = c.EndDate ?? c.StartDate,
            IsAllDay = true,
            ItemCategory = c.EntryType.ToString(),
            PriorityOrCriticality = "Normal",
            Status = c.Status.ToString(),
            OwnerOrAssignee = c.OwnerEmails ?? string.Empty,
            OwnerEmails = c.OwnerEmails,
            Comments = c.Comments,
            ParentTimelineId = c.ParentTimelineId,
            ColorHex = "#107c41",
            BadgeIcon = "Calendar",
            ScopeId = c.ProjectId,
            Level = TaskLevelEnum.Project
        }).ToList();

        // 2. Extensible plugin fan-out for work items
        // Sequential execution across shared AppDbContext to prevent concurrency violations
        var combinedRollups = new List<CalendarItemDto>();
        foreach (var provider in _rollupProviders)
        {
            var rollupItems = await provider.GetCalendarRollupItemsAsync(
                tenantId, TaskLevelEnum.Project, projectId, null, startUtc, endUtc, ct);
            combinedRollups.AddRange(rollupItems);
        }

        return directDtos.Concat(combinedRollups)
            .OrderBy(i => i.StartUtc)
            .ThenBy(i => i.EndUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetUserCalendarAsync(
        Guid tenantId,
        string userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        // 1. Direct user calendar entries
        Guid.TryParse(userId, out var userGuid);
        var directEntries = await _context.UserCalendars
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UserId == userGuid && !c.IsDeleted &&
                        c.StartDate <= endUtc && (c.EndDate ?? c.StartDate) >= startUtc)
            .ToListAsync(ct);

        var directDtos = directEntries.Select(c => new CalendarItemDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            StartUtc = c.StartDate,
            EndUtc = c.EndDate ?? c.StartDate,
            DueDate = c.DueDate,
            IsAllDay = true,
            ItemCategory = c.EntryType.ToString(),
            PriorityOrCriticality = "Normal",
            Status = c.Status.ToString(),
            OwnerOrAssignee = c.OwnerEmails ?? string.Empty,
            OwnerEmails = c.OwnerEmails,
            Comments = c.Comments,
            ParentTimelineId = c.ParentTimelineId,
            ColorHex = "#8764b8",
            BadgeIcon = "Calendar",
            Level = TaskLevelEnum.User
        }).ToList();

        // 2. Extensible plugin fan-out for work items
        // Sequential execution across shared AppDbContext to prevent concurrency violations
        var combinedRollups = new List<CalendarItemDto>();
        foreach (var provider in _rollupProviders)
        {
            var rollupItems = await provider.GetCalendarRollupItemsAsync(
                tenantId, TaskLevelEnum.User, null, userId, startUtc, endUtc, ct);
            combinedRollups.AddRange(rollupItems);
        }

        return directDtos.Concat(combinedRollups)
            .OrderBy(i => i.StartUtc)
            .ThenBy(i => i.EndUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ProgramCalendarEntity>> GetProgramCalendarEntitiesAsync(
        Guid tenantId,
        Guid programId,
        bool forEdit = false,
        Guid? revisionHeaderId = null,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var query = _context.ProgramCalendars
            .WithTracking(forEdit)
            .Where(c => c.ProgramId == programId && !c.IsDeleted);

        if (revisionHeaderId.HasValue && revisionHeaderId.Value != Guid.Empty)
        {
            query = query.Where(c => c.RevisionHeaderId == revisionHeaderId.Value);
        }
        else
        {
            query = query.Where(c => c.RevisionHeader!.Status == RevisionStatusEnum.Active);
        }

        if (tenantId != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == tenantId);
        }

        return await query.OrderBy(c => c.StartDate).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectCalendarEntity>> GetProjectCalendarEntitiesAsync(
        Guid tenantId,
        Guid projectId,
        bool forEdit = false,
        Guid? revisionHeaderId = null,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var query = _context.ProjectCalendars
            .WithTracking(forEdit)
            .Where(c => c.ProjectId == projectId && !c.IsDeleted);

        if (revisionHeaderId.HasValue && revisionHeaderId.Value != Guid.Empty)
        {
            query = query.Where(c => c.RevisionHeaderId == revisionHeaderId.Value);
        }
        else
        {
            query = query.Where(c => c.RevisionHeader!.Status == RevisionStatusEnum.Active);
        }

        if (tenantId != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == tenantId);
        }

        return await query.OrderBy(c => c.StartDate).ToListAsync(ct);
    }

    public async Task<Result<ProgramCalendarEntity>> CreateProgramCalendarEntryAsync(ProgramCalendarEntity entry, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (entry.RevisionHeaderId == Guid.Empty)

            {

                var activeHeader = await _context.Set<RevisionHeaderEntity>()

                    .AsNoTracking()

                    .FirstOrDefaultAsync(r => r.TenantId == entry.TenantId &&

                                              r.ScopeLevel == TaskLevelEnum.Program &&

                                              r.FeatureType == VersionedFeatureTypeEnum.ProgramCalendar &&

                                              r.ProgramId == entry.ProgramId && r.ProjectId == null &&

                                              (r.Status == RevisionStatusEnum.Active || r.Status == RevisionStatusEnum.Draft) &&

                                              !r.IsDeleted, ct);



                if (activeHeader == null)

                {

                    activeHeader = new RevisionHeaderEntity

                    {

                        TenantId = entry.TenantId,

                        ScopeLevel = TaskLevelEnum.Program,

                        FeatureType = VersionedFeatureTypeEnum.ProgramCalendar,

                        ProgramId = entry.ProgramId,

                        ProjectId = null,

                        VerNum = 1,

                        RevisionName = "Initial Baseline",

                        Status = RevisionStatusEnum.Active,

                        ActivatedAtUtc = DateTime.UtcNow,
                        CreatorEmail = _currentUser.Email ?? _currentUser.UserId ?? "system@local",

                        ParentVerNum = -1,

                        ParentRevisionHeaderId = Guid.Empty

                    };

                    await _context.Set<RevisionHeaderEntity>().AddAsync(activeHeader, ct);

                    await _context.SaveChangesAsync(ct);

                }



                entry.RevisionHeaderId = activeHeader.Id;

                entry.VerNum = activeHeader.VerNum;

            }



            if (entry.RelId == Guid.Empty)

            {

                entry.RelId = entry.Id != Guid.Empty ? entry.Id : Guid.NewGuid();

            }



            var existing = await _context.ProgramCalendars.AsTracking().FirstOrDefaultAsync(c => c.Id == entry.Id, ct);
            if (existing != null)
            {
                existing.ProgramId = entry.ProgramId;
                existing.ParentTimelineId = entry.ParentTimelineId;
                existing.Title = entry.Title;
                existing.Description = entry.Description;
                existing.OwnerEmails = entry.OwnerEmails;
                existing.StartDate = entry.StartDate;
                existing.EndDate = entry.EndDate;
                existing.EntryType = entry.EntryType;
                existing.Status = entry.Status;
                existing.Comments = entry.Comments;
            }
            else
            {
                await _context.ProgramCalendars.AddAsync(entry, ct);
            }
            await _context.SaveChangesAsync(ct);
            return Result<ProgramCalendarEntity>.Success(existing ?? entry);
        }
        catch (Exception ex)
        {
            if (_context.Entry(entry).State == EntityState.Added)
            {
                _context.Entry(entry).State = EntityState.Detached;
            }
            _logger.LogError(ex, "Failed to create program calendar entry for Program {ProgramId}", entry.ProgramId);
            return Result<ProgramCalendarEntity>.Failure($"Failed to create entry: {ex.Message}");
        }
    }

    public async Task<Result<ProjectCalendarEntity>> CreateProjectCalendarEntryAsync(ProjectCalendarEntity entry, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (entry.RevisionHeaderId == Guid.Empty)

            {

                var activeHeader = await _context.Set<RevisionHeaderEntity>()

                    .AsNoTracking()

                    .FirstOrDefaultAsync(r => r.TenantId == entry.TenantId &&

                                              r.ScopeLevel == TaskLevelEnum.Project &&

                                              r.FeatureType == VersionedFeatureTypeEnum.ProjectCalendar &&

                                              r.ProjectId == entry.ProjectId &&

                                              (r.Status == RevisionStatusEnum.Active || r.Status == RevisionStatusEnum.Draft) &&

                                              !r.IsDeleted, ct);



                if (activeHeader == null)

                {

                    var resolvedProgramId = entry.ProgramId;

                    if (resolvedProgramId == Guid.Empty)

                    {

                        var proj = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entry.ProjectId, ct);

                        resolvedProgramId = proj?.ProgramId ?? Guid.Empty;

                        entry.ProgramId = resolvedProgramId;

                    }



                    activeHeader = new RevisionHeaderEntity

                    {

                        TenantId = entry.TenantId,

                        ScopeLevel = TaskLevelEnum.Project,

                        FeatureType = VersionedFeatureTypeEnum.ProjectCalendar,

                        ProgramId = resolvedProgramId,

                        ProjectId = entry.ProjectId,

                        VerNum = 1,

                        RevisionName = "Initial Baseline",

                        Status = RevisionStatusEnum.Active,

                        ActivatedAtUtc = DateTime.UtcNow,
                        CreatorEmail = _currentUser.Email ?? _currentUser.UserId ?? "system@local",

                        ParentVerNum = -1,

                        ParentRevisionHeaderId = Guid.Empty

                    };

                    await _context.Set<RevisionHeaderEntity>().AddAsync(activeHeader, ct);

                    await _context.SaveChangesAsync(ct);

                }



                entry.RevisionHeaderId = activeHeader.Id;

                entry.VerNum = activeHeader.VerNum;

            }



            if (entry.RelId == Guid.Empty)

            {

                entry.RelId = entry.Id != Guid.Empty ? entry.Id : Guid.NewGuid();

            }



            var existing = await _context.ProjectCalendars.AsTracking().FirstOrDefaultAsync(c => c.Id == entry.Id, ct);
            if (existing != null)
            {
                existing.ProjectId = entry.ProjectId;
                existing.ParentProgramCalendarId = entry.ParentProgramCalendarId;
                existing.ProgramId = entry.ProgramId;
                existing.ParentTimelineId = entry.ParentTimelineId;
                existing.Title = entry.Title;
                existing.Description = entry.Description;
                existing.OwnerEmails = entry.OwnerEmails;
                existing.StartDate = entry.StartDate;
                existing.EndDate = entry.EndDate;
                existing.EntryType = entry.EntryType;
                existing.Status = entry.Status;
                existing.Comments = entry.Comments;
            }
            else
            {
                await _context.ProjectCalendars.AddAsync(entry, ct);
            }
            await _context.SaveChangesAsync(ct);
            return Result<ProjectCalendarEntity>.Success(existing ?? entry);
        }
        catch (Exception ex)
        {
            if (_context.Entry(entry).State == EntityState.Added)
            {
                _context.Entry(entry).State = EntityState.Detached;
            }
            _logger.LogError(ex, "Failed to create project calendar entry for Project {ProjectId}", entry.ProjectId);
            return Result<ProjectCalendarEntity>.Failure($"Failed to create entry: {ex.Message}");
        }
    }

    public async Task<Result<UserCalendarEntity>> CreateUserCalendarEntryAsync(UserCalendarEntity entry, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            await _context.UserCalendars.AddAsync(entry, ct);
            await _context.SaveChangesAsync(ct);
            return Result<UserCalendarEntity>.Success(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user calendar entry for User {UserId}", entry.UserId);
            return Result<UserCalendarEntity>.Failure($"Failed to create entry: {ex.Message}");
        }
    }

    public async Task<Result<bool>> DeleteCalendarEntryAsync(Guid entryId, TaskLevelEnum level, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (level == TaskLevelEnum.Program)
            {
                var entry = await _context.ProgramCalendars.AsTracking().FirstOrDefaultAsync(c => c.Id == entryId, ct);
                if (entry == null) return Result<bool>.Failure("Entry not found.");
                entry.IsDeleted = true;
                entry.DeletedAtUtc = DateTime.UtcNow;
                entry.DeletedBy = _currentUser.Email ?? _currentUser.UserId;
            }
            else if (level == TaskLevelEnum.Project)
            {
                var entry = await _context.ProjectCalendars.AsTracking().FirstOrDefaultAsync(c => c.Id == entryId, ct);
                if (entry == null) return Result<bool>.Failure("Entry not found.");
                entry.IsDeleted = true;
                entry.DeletedAtUtc = DateTime.UtcNow;
                entry.DeletedBy = _currentUser.Email ?? _currentUser.UserId;
            }
            else if (level == TaskLevelEnum.User)
            {
                var entry = await _context.UserCalendars.AsTracking().FirstOrDefaultAsync(c => c.Id == entryId, ct);
                if (entry == null) return Result<bool>.Failure("Entry not found.");
                entry.IsDeleted = true;
                entry.DeletedAtUtc = DateTime.UtcNow;
                entry.DeletedBy = _currentUser.Email ?? _currentUser.UserId;
            }

            await _context.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete calendar entry {EntryId}", entryId);
            return Result<bool>.Failure($"Failed to delete entry: {ex.Message}");
        }
    }
}
