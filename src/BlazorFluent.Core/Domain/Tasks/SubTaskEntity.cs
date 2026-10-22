using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Child subtask strictly governed under a parent <see cref="TaskEntity"/>.
/// Invariant: Maximum hierarchy depth is 1 (a subtask cannot have a subtask).
/// Invariant: Intra-task blockers can only reference sibling subtasks under the same ParentTaskId.
/// </summary>
public class SubTaskEntity : BaseTaskEntity
{
    /// <summary>
    /// Foreign key to the governing parent <see cref="TaskEntity"/>.
    /// Required.
    /// </summary>
    public required Guid ParentTaskId { get; set; }

    /// <summary>
    /// Relative display and execution order index within the parent task.
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// Optional intra-task blocker reference pointing to a sibling subtask under the same parent task.
    /// </summary>
    public Guid? BlockedBySubTaskId { get; set; }

    // --- Navigation Properties ---
    public TaskEntity? ParentTask { get; set; }
    public SubTaskEntity? BlockedBySubTask { get; set; }

    /// <summary>Subtask category discriminator for transition validation.</summary>
    public override string GetDefaultCategory() => TaskStatusEnumDefinitions.Categories.SubTask;
}
