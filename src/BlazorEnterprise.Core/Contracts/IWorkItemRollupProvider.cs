using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Extensible plugin provider interface allowing any BaseTaskEntity subtype
/// (Tasks, Subtasks, Risks, Assumptions, Issues, BlockerDependencies, or future extensions like Decisions)
/// to project its deadlines onto calendars and executive rollups without altering core calendar services.
/// </summary>
public interface IWorkItemRollupProvider
{
    /// <summary>
    /// Type of work item handled by this provider.
    /// </summary>
    TaskItemTypeEnum ItemType { get; }

    /// <summary>
    /// Queries the dedicated subtype table and yields unified CalendarItemDto instances for the specified window.
    /// </summary>
    Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        Guid tenantId,
        TaskLevelEnum level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default);
}
