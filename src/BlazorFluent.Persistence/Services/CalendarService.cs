using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class CalendarService : ICalendarService
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IWorkItemRollupProvider> _rollupProviders;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        AppDbContext context,
        IEnumerable<IWorkItemRollupProvider> rollupProviders,
        ICurrentUser currentUser,
        ILogger<CalendarService> logger)
    {
        _context = context;
        _rollupProviders = rollupProviders;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetProgramCalendarAsync(
        string tenantId,
        Guid programId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        // 1. Direct program calendar entries
        var directEntries = await _context.ProgramCalendars
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ProgramId == programId && !c.IsDeleted &&
                        c.StartUtc <= endUtc && c.EndUtc >= startUtc)
            .ToListAsync(ct);

        var directDtos = directEntries.Select(c => new CalendarItemDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            StartUtc = c.StartUtc,
            EndUtc = c.EndUtc,
            IsAllDay = c.IsAllDay,
            ItemCategory = c.EntryType.ToString(),
            PriorityOrCriticality = "Normal",
            Status = c.Status.ToString(),
            OwnerOrAssignee = c.OrganizerEmail,
            ColorHex = "#0078d4",
            BadgeIcon = "Calendar",
            ScopeId = c.ProgramId,
            Level = TaskLevel.Program
        }).ToList();

        // 2. Extensible plugin fan-out for work items
        var rollupTasks = _rollupProviders.Select(p =>
            p.GetCalendarRollupItemsAsync(tenantId, TaskLevel.Program, programId, null, startUtc, endUtc, ct));

        var rollupResults = await Task.WhenAll(rollupTasks);
        var combinedRollups = rollupResults.SelectMany(r => r).ToList();

        return directDtos.Concat(combinedRollups)
            .OrderBy(i => i.StartUtc)
            .ThenBy(i => i.EndUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetProjectCalendarAsync(
        string tenantId,
        Guid projectId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        // 1. Direct project calendar entries
        var directEntries = await _context.ProjectCalendars
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ProjectId == projectId && !c.IsDeleted &&
                        c.StartUtc <= endUtc && c.EndUtc >= startUtc)
            .ToListAsync(ct);

        var directDtos = directEntries.Select(c => new CalendarItemDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            StartUtc = c.StartUtc,
            EndUtc = c.EndUtc,
            IsAllDay = c.IsAllDay,
            ItemCategory = c.EntryType.ToString(),
            PriorityOrCriticality = "Normal",
            Status = c.Status.ToString(),
            OwnerOrAssignee = c.OrganizerEmail,
            ColorHex = "#107c41",
            BadgeIcon = "Calendar",
            ScopeId = c.ProjectId,
            Level = TaskLevel.Project
        }).ToList();

        // 2. Extensible plugin fan-out for work items
        var rollupTasks = _rollupProviders.Select(p =>
            p.GetCalendarRollupItemsAsync(tenantId, TaskLevel.Project, projectId, null, startUtc, endUtc, ct));

        var rollupResults = await Task.WhenAll(rollupTasks);
        var combinedRollups = rollupResults.SelectMany(r => r).ToList();

        return directDtos.Concat(combinedRollups)
            .OrderBy(i => i.StartUtc)
            .ThenBy(i => i.EndUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetUserCalendarAsync(
        string tenantId,
        string userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        // 1. Direct user calendar entries
        var directEntries = await _context.UserCalendars
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UserId == userId && !c.IsDeleted &&
                        c.StartUtc <= endUtc && c.EndUtc >= startUtc)
            .ToListAsync(ct);

        var directDtos = directEntries.Select(c => new CalendarItemDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            StartUtc = c.StartUtc,
            EndUtc = c.EndUtc,
            IsAllDay = c.IsAllDay,
            ItemCategory = c.EntryType.ToString(),
            PriorityOrCriticality = "Normal",
            Status = c.Status.ToString(),
            OwnerOrAssignee = c.OrganizerEmail,
            ColorHex = "#8764b8",
            BadgeIcon = "Calendar",
            Level = TaskLevel.User
        }).ToList();

        // 2. Extensible plugin fan-out for work items
        var rollupTasks = _rollupProviders.Select(p =>
            p.GetCalendarRollupItemsAsync(tenantId, TaskLevel.User, null, userId, startUtc, endUtc, ct));

        var rollupResults = await Task.WhenAll(rollupTasks);
        var combinedRollups = rollupResults.SelectMany(r => r).ToList();

        return directDtos.Concat(combinedRollups)
            .OrderBy(i => i.StartUtc)
            .ThenBy(i => i.EndUtc)
            .ToList();
    }

    public async Task<Result<ProgramCalendarEntity>> CreateProgramCalendarEntryAsync(ProgramCalendarEntity entry, CancellationToken ct = default)
    {
        try
        {
            await _context.ProgramCalendars.AddAsync(entry, ct);
            await _context.SaveChangesAsync(ct);
            return Result<ProgramCalendarEntity>.Success(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create program calendar entry for Program {ProgramId}", entry.ProgramId);
            return Result<ProgramCalendarEntity>.Failure($"Failed to create entry: {ex.Message}");
        }
    }

    public async Task<Result<ProjectCalendarEntity>> CreateProjectCalendarEntryAsync(ProjectCalendarEntity entry, CancellationToken ct = default)
    {
        try
        {
            await _context.ProjectCalendars.AddAsync(entry, ct);
            await _context.SaveChangesAsync(ct);
            return Result<ProjectCalendarEntity>.Success(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project calendar entry for Project {ProjectId}", entry.ProjectId);
            return Result<ProjectCalendarEntity>.Failure($"Failed to create entry: {ex.Message}");
        }
    }

    public async Task<Result<UserCalendarEntity>> CreateUserCalendarEntryAsync(UserCalendarEntity entry, CancellationToken ct = default)
    {
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

    public async Task<Result<bool>> DeleteCalendarEntryAsync(Guid entryId, TaskLevel level, CancellationToken ct = default)
    {
        try
        {
            if (level == TaskLevel.Program)
            {
                var entry = await _context.ProgramCalendars.FirstOrDefaultAsync(c => c.Id == entryId, ct);
                if (entry == null) return Result<bool>.Failure("Entry not found.");
                entry.IsDeleted = true;
                entry.DeletedAtUtc = DateTime.UtcNow;
                entry.DeletedBy = _currentUser.Email ?? _currentUser.UserId;
            }
            else if (level == TaskLevel.Project)
            {
                var entry = await _context.ProjectCalendars.FirstOrDefaultAsync(c => c.Id == entryId, ct);
                if (entry == null) return Result<bool>.Failure("Entry not found.");
                entry.IsDeleted = true;
                entry.DeletedAtUtc = DateTime.UtcNow;
                entry.DeletedBy = _currentUser.Email ?? _currentUser.UserId;
            }
            else if (level == TaskLevel.User)
            {
                var entry = await _context.UserCalendars.FirstOrDefaultAsync(c => c.Id == entryId, ct);
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
