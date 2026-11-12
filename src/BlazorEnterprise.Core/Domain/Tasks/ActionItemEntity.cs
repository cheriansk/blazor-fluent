using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Granular checklist or action item attached directly to a <see cref="BaseTaskEntity"/>
/// (e.g. for ActivityTasks or RAID items) or grouped optionally under a <see cref="SubTaskEntity"/>
/// (e.g. for SprintTask file-level or subtask organization).
/// </summary>
public class ActionItemEntity : ProjectScopedEntity, ISoftDeletableEntity, IUnversionedEntity
{
    /// <summary>
    /// Required foreign key to the governing parent task (e.g. ActivityTask, SprintTask, Risk, etc.).
    /// Cascade deleted when the parent task is removed.
    /// </summary>
    public required Guid ParentTaskId { get; set; }

    /// <summary>
    /// Optional foreign key grouping this checklist item under a specific child subtask.
    /// If null, this action item belongs directly to the task.
    /// </summary>
    public Guid? ParentSubTaskId { get; set; }

    /// <summary>
    /// Headline title of the action item. Required, 1 to 500 characters.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional detailed description or notes (up to 2000 characters).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Lifecycle status of this checklist item.
    /// </summary>
    public ActionItemStatusEnum Status { get; set; }

    /// <summary>
    /// Complexity score (1-5 points).
    /// </summary>
    public int Complexity { get; set; }

    /// <summary>
    /// Estimated effort in hours (must be >= 0).
    /// </summary>
    public decimal EstimatedHours { get; set; }

    /// <summary>
    /// Relative display and execution ordering index within the task or subtask level.
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// Whether this action item is currently blocked.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// Reason explaining why the item is blocked (required if IsBlocked is true, min 10 characters).
    /// </summary>
    public string? BlockingReason { get; set; }

    // --- Soft Delete ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    // Polymorphic parent task reference ID: ParentTaskId
    // Optional parent subtask reference ID: ParentSubTaskId
}
