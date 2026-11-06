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

    private readonly IRevisionService _revisionService;

    public CalendarService(
        IDbContextFactory<AppDbContext> dbFactory,
        IEnumerable<IWorkItemRollupProvider> rollupProviders,
        ICurrentUser currentUser,
        IRevisionService revisionService,
        ILogger<CalendarService> logger)
    {
        _dbFactory = dbFactory;
        _rollupProviders = rollupProviders;
        _currentUser = currentUser;
        _revisionService = revisionService;
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

    public async Task<Result<int>> SaveProgramCalendarScheduleAsync(
        Guid tenantId,
        Guid programId,
        IReadOnlyList<BlazorEnterprise.Core.Dtos.ProgramTimelineRowDto> timelineRows,
        Guid? revisionHeaderId = null,
        bool isBatchDraft = false,
        string? batchRevisionName = null,
        CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var actingUser = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId ?? "system@local";
            var now = DateTime.UtcNow;

            RevisionHeaderEntity targetHeader;
            if (isBatchDraft)
            {
                // Batch always creates a new Draft scenario revision
                targetHeader = await _revisionService.CreateDraftAsync(
                    scopeLevel: TaskLevelEnum.Program,
                    featureType: VersionedFeatureTypeEnum.ProgramCalendar,
                    revisionName: batchRevisionName ?? $"Batch Import - {now:yyyy-MM-dd HH:mm}",
                    sourceRevisionHeaderId: revisionHeaderId,
                    programId: programId,
                    projectId: null,
                    isBatch: true,
                    cancellationToken: ct);
            }
            else if (revisionHeaderId.HasValue && revisionHeaderId.Value != Guid.Empty)
            {
                var header = await context.RevisionHeaders.FirstOrDefaultAsync(r => r.Id == revisionHeaderId.Value && r.TenantId == tenantId, ct);
                if (header == null)
                {
                    return Result<int>.Failure($"Revision header '{revisionHeaderId.Value}' was not found.");
                }
                targetHeader = header;
            }
            else
            {
                // UI Baseline fallback
                var activeHeader = await context.RevisionHeaders.FirstOrDefaultAsync(
                    r => r.TenantId == tenantId && r.ScopeLevel == TaskLevelEnum.Program &&
                         r.FeatureType == VersionedFeatureTypeEnum.ProgramCalendar && r.ProgramId == programId &&
                         (r.Status == RevisionStatusEnum.Active || r.Status == RevisionStatusEnum.Draft) && !r.IsDeleted, ct);

                if (activeHeader == null)
                {
                    activeHeader = new RevisionHeaderEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        ScopeLevel = TaskLevelEnum.Program,
                        FeatureType = VersionedFeatureTypeEnum.ProgramCalendar,
                        ProgramId = programId,
                        VerNum = 1,
                        RevisionName = "Initial Baseline",
                        Status = RevisionStatusEnum.Active,
                        ActivatedAtUtc = now,
                        CreatorEmail = actingUser,
                        Created = now,
                        CreatedBy = actingUser,
                        Updated = now,
                        UpdatedBy = actingUser
                    };
                    context.RevisionHeaders.Add(activeHeader);
                    await context.SaveChangesAsync(ct);
                }
                targetHeader = activeHeader;
            }

            // Existing rows in target revision (or active revision for RelId lineage matching)
            var existingRows = await context.ProgramCalendars
                .Where(c => c.TenantId == tenantId && c.ProgramId == programId && !c.IsDeleted)
                .ToListAsync(ct);

            var activeBaselineRows = existingRows.Where(r => r.RevisionHeaderId == targetHeader.ParentRevisionHeaderId).ToList();

            var activeTimelineIds = new HashSet<Guid>();
            var activeMilestoneIds = new HashSet<Guid>();
            var rowsCommitted = 0;

            if (timelineRows != null)
            {
                foreach (var row in timelineRows)
                {
                    var rowId = row.Id == Guid.Empty ? Guid.CreateVersion7() : row.Id;
                    activeTimelineIds.Add(rowId);
                    var rowStatus = row.Status.ToEnum(CalendarEntryStatusEnum.New);

                    // Lineage RelId matching
                    var matchedActiveTimeline = activeBaselineRows.FirstOrDefault(r =>
                        r.EntryType == CalendarEntryTypeEnum.Timeline &&
                        string.Equals(r.Title, row.Title, StringComparison.OrdinalIgnoreCase));

                    var timelineRelId = matchedActiveTimeline?.RelId ?? Guid.CreateVersion7();
                    var timelineParentRowId = matchedActiveTimeline?.Id ?? Guid.Empty;

                    var existingTimeline = existingRows.FirstOrDefault(c => c.Id == rowId && c.RevisionHeaderId == targetHeader.Id);
                    if (existingTimeline != null)
                    {
                        existingTimeline.Title = row.Title;
                        existingTimeline.StartDate = row.StartDate ?? now.Date;
                        existingTimeline.EndDate = row.EndDate;
                        existingTimeline.Status = rowStatus;
                        existingTimeline.Comments = row.Comments;
                        existingTimeline.Updated = now;
                        existingTimeline.UpdatedBy = actingUser;
                    }
                    else
                    {
                        var newTimeline = new ProgramCalendarEntity
                        {
                            Id = rowId,
                            TenantId = tenantId,
                            ProgramId = programId,
                            RevisionHeaderId = targetHeader.Id,
                            RelId = timelineRelId,
                            VerNum = targetHeader.VerNum,
                            ParentRowId = timelineParentRowId,
                            Title = row.Title,
                            StartDate = row.StartDate ?? now.Date,
                            EndDate = row.EndDate,
                            Status = rowStatus,
                            Comments = row.Comments,
                            EntryType = CalendarEntryTypeEnum.Timeline,
                            Created = now,
                            CreatedBy = actingUser,
                            Updated = now,
                            UpdatedBy = actingUser
                        };
                        context.ProgramCalendars.Add(newTimeline);
                    }
                    rowsCommitted++;

                    foreach (var m in row.Milestones ?? [])
                    {
                        var milestoneId = m.Id == Guid.Empty ? Guid.CreateVersion7() : m.Id;
                        activeMilestoneIds.Add(milestoneId);
                        var mStatus = m.Status.ToEnum(CalendarEntryStatusEnum.New);

                        var matchedActiveMilestone = activeBaselineRows.FirstOrDefault(r =>
                            r.EntryType == CalendarEntryTypeEnum.Milestone &&
                            string.Equals(r.Title, m.Title, StringComparison.OrdinalIgnoreCase));

                        var milestoneRelId = matchedActiveMilestone?.RelId ?? Guid.CreateVersion7();
                        var milestoneParentRowId = matchedActiveMilestone?.Id ?? Guid.Empty;

                        var existingMilestone = existingRows.FirstOrDefault(c => c.Id == milestoneId && c.RevisionHeaderId == targetHeader.Id);
                        if (existingMilestone != null)
                        {
                            existingMilestone.ParentTimelineId = rowId;
                            existingMilestone.Title = m.Title;
                            existingMilestone.StartDate = m.StartDate ?? row.StartDate ?? now.Date;
                            existingMilestone.EndDate = m.EndDate ?? row.EndDate ?? now.Date;
                            existingMilestone.OwnerEmails = m.OwnerEmails;
                            existingMilestone.Status = mStatus;
                            existingMilestone.Comments = m.Comments;
                            existingMilestone.Updated = now;
                            existingMilestone.UpdatedBy = actingUser;
                        }
                        else
                        {
                            var newMilestone = new ProgramCalendarEntity
                            {
                                Id = milestoneId,
                                TenantId = tenantId,
                                ProgramId = programId,
                                RevisionHeaderId = targetHeader.Id,
                                RelId = milestoneRelId,
                                VerNum = targetHeader.VerNum,
                                ParentRowId = milestoneParentRowId,
                                ParentTimelineId = rowId,
                                Title = m.Title,
                                StartDate = m.StartDate ?? row.StartDate ?? now.Date,
                                EndDate = m.EndDate ?? row.EndDate ?? now.Date,
                                OwnerEmails = m.OwnerEmails,
                                Status = mStatus,
                                Comments = m.Comments,
                                EntryType = CalendarEntryTypeEnum.Milestone,
                                Created = now,
                                CreatedBy = actingUser,
                                Updated = now,
                                UpdatedBy = actingUser
                            };
                            context.ProgramCalendars.Add(newMilestone);
                        }
                        rowsCommitted++;
                    }
                }
            }

            // Soft-delete removed rows if editing an existing revision
            if (!isBatchDraft)
            {
                var targetRevisionExisting = existingRows.Where(r => r.RevisionHeaderId == targetHeader.Id).ToList();
                foreach (var entry in targetRevisionExisting)
                {
                    if (entry.EntryType == CalendarEntryTypeEnum.Timeline && !activeTimelineIds.Contains(entry.Id))
                    {
                        entry.IsDeleted = true;
                        entry.DeletedAtUtc = now;
                        entry.DeletedBy = actingUser;
                    }
                    else if (entry.EntryType == CalendarEntryTypeEnum.Milestone && !activeMilestoneIds.Contains(entry.Id))
                    {
                        entry.IsDeleted = true;
                        entry.DeletedAtUtc = now;
                        entry.DeletedBy = actingUser;
                    }
                }
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Saved Program {ProgramId} Calendar schedule. Draft: {IsBatchDraft}, VerNum: v{VerNum}, Committed {Count} rows.",
                programId, isBatchDraft, targetHeader.VerNum, rowsCommitted);

            return Result<int>.Success(rowsCommitted);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to persist Program Calendar schedule for Program {ProgramId}.", programId);
            return Result<int>.Failure($"Failed to persist program calendar: {ex.Message}");
        }
    }

    public async Task<Result<int>> SaveProjectCalendarScheduleAsync(
        Guid tenantId,
        Guid projectId,
        Guid? programId,
        IReadOnlyList<BlazorEnterprise.Core.Dtos.ProjectPhaseDto> phases,
        Guid? revisionHeaderId = null,
        bool isBatchDraft = false,
        string? batchRevisionName = null,
        CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var actingUser = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId ?? "system@local";
            var now = DateTime.UtcNow;

            var resolvedProgramId = programId ?? Guid.Empty;
            if (resolvedProgramId == Guid.Empty)
            {
                var proj = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId, ct);
                resolvedProgramId = proj?.ProgramId ?? Guid.Empty;
            }

            RevisionHeaderEntity targetHeader;
            if (isBatchDraft)
            {
                // Batch always creates a new Draft scenario revision
                targetHeader = await _revisionService.CreateDraftAsync(
                    scopeLevel: TaskLevelEnum.Project,
                    featureType: VersionedFeatureTypeEnum.ProjectCalendar,
                    revisionName: batchRevisionName ?? $"Batch Import - {now:yyyy-MM-dd HH:mm}",
                    sourceRevisionHeaderId: revisionHeaderId,
                    programId: resolvedProgramId,
                    projectId: projectId,
                    isBatch: true,
                    cancellationToken: ct);
            }
            else if (revisionHeaderId.HasValue && revisionHeaderId.Value != Guid.Empty)
            {
                var header = await context.RevisionHeaders.FirstOrDefaultAsync(r => r.Id == revisionHeaderId.Value && r.TenantId == tenantId, ct);
                if (header == null)
                {
                    return Result<int>.Failure($"Revision header '{revisionHeaderId.Value}' was not found.");
                }
                targetHeader = header;
            }
            else
            {
                var activeHeader = await context.RevisionHeaders.FirstOrDefaultAsync(
                    r => r.TenantId == tenantId && r.ScopeLevel == TaskLevelEnum.Project &&
                         r.FeatureType == VersionedFeatureTypeEnum.ProjectCalendar && r.ProjectId == projectId &&
                         (r.Status == RevisionStatusEnum.Active || r.Status == RevisionStatusEnum.Draft) && !r.IsDeleted, ct);

                if (activeHeader == null)
                {
                    activeHeader = new RevisionHeaderEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        ScopeLevel = TaskLevelEnum.Project,
                        FeatureType = VersionedFeatureTypeEnum.ProjectCalendar,
                        ProgramId = resolvedProgramId,
                        ProjectId = projectId,
                        VerNum = 1,
                        RevisionName = "Initial Baseline",
                        Status = RevisionStatusEnum.Active,
                        ActivatedAtUtc = now,
                        CreatorEmail = actingUser,
                        Created = now,
                        CreatedBy = actingUser,
                        Updated = now,
                        UpdatedBy = actingUser
                    };
                    context.RevisionHeaders.Add(activeHeader);
                    await context.SaveChangesAsync(ct);
                }
                targetHeader = activeHeader;
            }

            var existingRows = await context.ProjectCalendars
                .Where(c => c.TenantId == tenantId && c.ProjectId == projectId && !c.IsDeleted)
                .ToListAsync(ct);

            var activeBaselineRows = existingRows.Where(r => r.RevisionHeaderId == targetHeader.ParentRevisionHeaderId).ToList();

            var activePhaseIds = new HashSet<Guid>();
            var activeMilestoneIds = new HashSet<Guid>();
            var rowsCommitted = 0;

            if (phases != null)
            {
                foreach (var phase in phases)
                {
                    var phaseId = phase.Id == Guid.Empty ? Guid.CreateVersion7() : phase.Id;
                    activePhaseIds.Add(phaseId);
                    var phaseStatus = phase.Status.ToEnum(CalendarEntryStatusEnum.New);

                    var matchedActivePhase = activeBaselineRows.FirstOrDefault(r =>
                        r.EntryType == CalendarEntryTypeEnum.Timeline &&
                        string.Equals(r.Title, phase.Title, StringComparison.OrdinalIgnoreCase));

                    var phaseRelId = matchedActivePhase?.RelId ?? Guid.CreateVersion7();
                    var phaseParentRowId = matchedActivePhase?.Id ?? Guid.Empty;

                    var existingPhase = existingRows.FirstOrDefault(c => c.Id == phaseId && c.RevisionHeaderId == targetHeader.Id);
                    if (existingPhase != null)
                    {
                        existingPhase.Title = phase.Title;
                        existingPhase.StartDate = phase.StartDate ?? now.Date;
                        existingPhase.EndDate = phase.EndDate;
                        existingPhase.Status = phaseStatus;
                        existingPhase.Comments = phase.Comments;
                        existingPhase.Updated = now;
                        existingPhase.UpdatedBy = actingUser;
                    }
                    else
                    {
                        var newPhase = new ProjectCalendarEntity
                        {
                            Id = phaseId,
                            TenantId = tenantId,
                            ProjectId = projectId,
                            ProgramId = resolvedProgramId,
                            RevisionHeaderId = targetHeader.Id,
                            RelId = phaseRelId,
                            VerNum = targetHeader.VerNum,
                            ParentRowId = phaseParentRowId,
                            Title = phase.Title,
                            StartDate = phase.StartDate ?? now.Date,
                            EndDate = phase.EndDate,
                            Status = phaseStatus,
                            Comments = phase.Comments,
                            EntryType = CalendarEntryTypeEnum.Timeline,
                            Created = now,
                            CreatedBy = actingUser,
                            Updated = now,
                            UpdatedBy = actingUser
                        };
                        context.ProjectCalendars.Add(newPhase);
                    }
                    rowsCommitted++;

                    foreach (var m in phase.Milestones ?? [])
                    {
                        var milestoneId = m.Id == Guid.Empty ? Guid.CreateVersion7() : m.Id;
                        activeMilestoneIds.Add(milestoneId);
                        var mStatus = m.Status.ToEnum(CalendarEntryStatusEnum.New);

                        var matchedActiveMilestone = activeBaselineRows.FirstOrDefault(r =>
                            r.EntryType == CalendarEntryTypeEnum.Milestone &&
                            string.Equals(r.Title, m.Title, StringComparison.OrdinalIgnoreCase));

                        var milestoneRelId = matchedActiveMilestone?.RelId ?? Guid.CreateVersion7();
                        var milestoneParentRowId = matchedActiveMilestone?.Id ?? Guid.Empty;

                        var existingMilestone = existingRows.FirstOrDefault(c => c.Id == milestoneId && c.RevisionHeaderId == targetHeader.Id);
                        if (existingMilestone != null)
                        {
                            existingMilestone.ParentTimelineId = phaseId;
                            existingMilestone.Title = m.Title;
                            existingMilestone.StartDate = m.StartDate ?? phase.StartDate ?? now.Date;
                            existingMilestone.EndDate = m.EndDate ?? phase.EndDate ?? now.Date;
                            existingMilestone.OwnerEmails = m.OwnerEmails;
                            existingMilestone.Status = mStatus;
                            existingMilestone.Comments = m.Comments;
                            existingMilestone.Updated = now;
                            existingMilestone.UpdatedBy = actingUser;
                        }
                        else
                        {
                            var newMilestone = new ProjectCalendarEntity
                            {
                                Id = milestoneId,
                                TenantId = tenantId,
                                ProjectId = projectId,
                                ProgramId = resolvedProgramId,
                                RevisionHeaderId = targetHeader.Id,
                                RelId = milestoneRelId,
                                VerNum = targetHeader.VerNum,
                                ParentRowId = milestoneParentRowId,
                                ParentTimelineId = phaseId,
                                Title = m.Title,
                                StartDate = m.StartDate ?? phase.StartDate ?? now.Date,
                                EndDate = m.EndDate ?? phase.EndDate ?? now.Date,
                                OwnerEmails = m.OwnerEmails,
                                Status = mStatus,
                                Comments = m.Comments,
                                EntryType = CalendarEntryTypeEnum.Milestone,
                                Created = now,
                                CreatedBy = actingUser,
                                Updated = now,
                                UpdatedBy = actingUser
                            };
                            context.ProjectCalendars.Add(newMilestone);
                        }
                        rowsCommitted++;
                    }
                }
            }

            if (!isBatchDraft)
            {
                var targetRevisionExisting = existingRows.Where(r => r.RevisionHeaderId == targetHeader.Id).ToList();
                foreach (var entry in targetRevisionExisting)
                {
                    if (entry.EntryType == CalendarEntryTypeEnum.Timeline && !activePhaseIds.Contains(entry.Id))
                    {
                        entry.IsDeleted = true;
                        entry.DeletedAtUtc = now;
                        entry.DeletedBy = actingUser;
                    }
                    else if (entry.EntryType == CalendarEntryTypeEnum.Milestone && !activeMilestoneIds.Contains(entry.Id))
                    {
                        entry.IsDeleted = true;
                        entry.DeletedAtUtc = now;
                        entry.DeletedBy = actingUser;
                    }
                }
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Saved Project {ProjectId} Calendar schedule. Draft: {IsBatchDraft}, VerNum: v{VerNum}, Committed {Count} rows.",
                projectId, isBatchDraft, targetHeader.VerNum, rowsCommitted);

            return Result<int>.Success(rowsCommitted);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to persist Project Calendar schedule for Project {ProjectId}.", projectId);
            return Result<int>.Failure($"Failed to persist project calendar: {ex.Message}");
        }
    }
}
