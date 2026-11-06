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

    // --- Direct Entity Retrieval ---
    Task<IReadOnlyList<ProgramCalendarEntity>> GetProgramCalendarEntitiesAsync(
        Guid tenantId,
        Guid programId,
        bool forEdit = false,
        Guid? revisionHeaderId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProjectCalendarEntity>> GetProjectCalendarEntitiesAsync(
        Guid tenantId,
        Guid projectId,
        bool forEdit = false,
        Guid? revisionHeaderId = null,
        CancellationToken ct = default);

    // --- Direct Calendar Operations ---
    Task<Result<ProgramCalendarEntity>> CreateProgramCalendarEntryAsync(ProgramCalendarEntity entry, CancellationToken ct = default);
    Task<Result<ProjectCalendarEntity>> CreateProjectCalendarEntryAsync(ProjectCalendarEntity entry, CancellationToken ct = default);
    Task<Result<UserCalendarEntity>> CreateUserCalendarEntryAsync(UserCalendarEntity entry, CancellationToken ct = default);

    Task<Result<bool>> DeleteCalendarEntryAsync(Guid entryId, TaskLevelEnum level, CancellationToken ct = default);

    // --- Enterprise Scenario Revision & Schedule Persistence (Shared between UI & Batch) ---
    Task<Result<int>> SaveProgramCalendarScheduleAsync(
        Guid tenantId,
        Guid programId,
        IReadOnlyList<BlazorEnterprise.Core.Dtos.ProgramTimelineRowDto> timelineRows,
        Guid? revisionHeaderId = null,
        bool isBatchDraft = false,
        string? batchRevisionName = null,
        CancellationToken ct = default);

    Task<Result<int>> SaveProjectCalendarScheduleAsync(
        Guid tenantId,
        Guid projectId,
        Guid? programId,
        IReadOnlyList<BlazorEnterprise.Core.Dtos.ProjectPhaseDto> phases,
        Guid? revisionHeaderId = null,
        bool isBatchDraft = false,
        string? batchRevisionName = null,
        CancellationToken ct = default);
}
