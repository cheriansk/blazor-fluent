using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Dtos.Response;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Unified calendar management and aggregation service.
/// Queries dedicated calendar tables (Program, Project, User) and concurrently aggregates
/// derived work item deadlines via registered IWorkItemRollupProvider plugins.
/// </summary>
public interface ICalendarService
{
    // --- Unified Rollup Streams ---
    Task<IReadOnlyList<CalendarItemDto>> GetProgramCalendarAsync(
        Guid tenantId,
        Guid programId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default);

    Task<IReadOnlyList<CalendarItemDto>> GetProjectCalendarAsync(
        Guid tenantId,
        Guid projectId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default);

    Task<IReadOnlyList<CalendarItemDto>> GetUserCalendarAsync(
        Guid tenantId,
        string userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default);

    // --- Direct Calendar Operations ---
    Task<Result<ProgramCalendarEntity>> CreateProgramCalendarEntryAsync(ProgramCalendarEntity entry, CancellationToken ct = default);
    Task<Result<ProjectCalendarEntity>> CreateProjectCalendarEntryAsync(ProjectCalendarEntity entry, CancellationToken ct = default);
    Task<Result<UserCalendarEntity>> CreateUserCalendarEntryAsync(UserCalendarEntity entry, CancellationToken ct = default);

    Task<Result<bool>> DeleteCalendarEntryAsync(Guid entryId, TaskLevelEnum level, CancellationToken ct = default);
}
