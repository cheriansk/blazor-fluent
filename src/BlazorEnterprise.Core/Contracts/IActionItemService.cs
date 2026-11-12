using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos.Tasks;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Service contract for managing checklist action items attached directly to tasks or grouped under subtasks.
/// </summary>
public interface IActionItemService
{
    // Create
    Task<ActionItemDto> CreateActionItemAsync(Guid taskId, CreateActionItemDto dto, CancellationToken ct = default);
    Task<ActionItemDto> CreateActionItemUnderSubTaskAsync(Guid taskId, Guid subTaskId, CreateActionItemDto dto, CancellationToken ct = default);

    // Update
    Task<ActionItemDto> UpdateActionItemAsync(Guid actionItemId, UpdateActionItemDto dto, CancellationToken ct = default);
    Task<ActionItemDto> MoveActionItemAsync(Guid actionItemId, Guid? newSubTaskId, CancellationToken ct = default);
    Task<ActionItemDto> ReorderActionItemAsync(Guid actionItemId, int newOrderIndex, CancellationToken ct = default);

    // Status & Blocking
    Task<ActionItemDto> UpdateStatusAsync(Guid actionItemId, ActionItemStatusEnum status, CancellationToken ct = default);
    Task<ActionItemDto> ToggleBlockingAsync(Guid actionItemId, bool isBlocked, string? reason = null, CancellationToken ct = default);

    // Query
    Task<IReadOnlyList<ActionItemDto>> GetActionItemsByTaskAsync(Guid taskId, bool includeSubTaskItems = false, CancellationToken ct = default);
    Task<IReadOnlyList<ActionItemDto>> GetActionItemsBySubTaskAsync(Guid subTaskId, CancellationToken ct = default);
    Task<ActionItemDto?> GetActionItemAsync(Guid actionItemId, CancellationToken ct = default);
    Task<IReadOnlyList<ActionItemDto>> GetBlockedItemsAsync(Guid taskId, CancellationToken ct = default);

    // Delete
    Task DeleteActionItemAsync(Guid actionItemId, CancellationToken ct = default);
    Task DeleteActionItemsBySubTaskAsync(Guid subTaskId, CancellationToken ct = default);

    // Calculations & Reporting
    Task<int> GetActionItemCountAsync(Guid taskId, bool includeSubTasks = false, CancellationToken ct = default);
    Task<decimal> GetCompletionPercentageAsync(Guid taskId, bool includeSubTasks = false, CancellationToken ct = default);
    Task<int> GetBlockedCountAsync(Guid taskId, CancellationToken ct = default);
    Task<int> GetComplexityTotalAsync(Guid taskId, bool includeSubTasks = false, CancellationToken ct = default);
}
