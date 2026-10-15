using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Contracts;

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
    TaskItemType ItemType { get; }

    /// <summary>
    /// Queries the dedicated subtype table and yields unified CalendarItemDto instances for the specified window.
    /// </summary>
    Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        string tenantId,
        TaskLevel level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default);
}
