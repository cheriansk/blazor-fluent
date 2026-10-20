using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Universal enterprise dependency contract ("D" of RAID).
/// Single intermediate table modeling all dependencies across the enterprise:
/// intra-project, cross-project (same program or cross-program), cross-level (Program/Project),
/// task-to-task, gate-to-gate, task-to-gate, and manual external vendor dependencies.
/// Side B (Dependent item waiting) depends on Side A (Prerequisite item blocking).
/// Subtype of <see cref="BaseTaskEntity"/> mapped to table 'tasks.work_dependencies'.
/// </summary>
public class WorkDependencyEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    // =========================================================================
    // Side B: Dependent / Successor Item (Waiting on Prerequisite A)
    // Note: Level, ProjectId, ProgramId are inherited from BaseTaskEntity.
    // =========================================================================

    /// <summary>
    /// Endpoint classification for Side B (CalendarGate, TaskItem, ManualDependency).
    /// </summary>
    public DependencyNodeTypeEnum DependentType { get; set; } = DependencyNodeTypeEnum.TaskItem;

    /// <summary>
    /// ID of the dependent entity (Task ID if TaskItem, Calendar ID if CalendarGate, null if general RAID ManualDependency).
    /// </summary>
    public Guid? DependentItemId { get; set; }

    /// <summary>
    /// Backward-compatibility alias forwarding to <see cref="DependentItemId"/>.
    /// </summary>
    [NotMapped]
    public Guid? SourceTaskId
    {
        get => DependentItemId;
        set => DependentItemId = value;
    }

    // =========================================================================
    // Side A: Prerequisite / Predecessor Item (Blocking Dependent B)
    // =========================================================================

    /// <summary>
    /// Endpoint classification for Side A (CalendarGate, TaskItem, ManualDependency).
    /// </summary>
    public DependencyNodeTypeEnum DependsOnType { get; set; } = DependencyNodeTypeEnum.TaskItem;

    /// <summary>
    /// ID of the prerequisite entity (Task ID if TaskItem, Calendar ID if CalendarGate, null if ManualDependency).
    /// </summary>
    public Guid? DependsOnItemId { get; set; }

    /// <summary>
    /// Hierarchy scoping level of Side A (Program or Project).
    /// </summary>
    public TaskLevel DependsOnLevel { get; set; } = TaskLevel.Project;

    /// <summary>
    /// Target project ID of Side A (supports intra-project or cross-project within/across programs).
    /// </summary>
    public Guid? DependsOnProjectId { get; set; }

    /// <summary>
    /// Target program ID of Side A (supports cross-program or program-level dependencies).
    /// </summary>
    public Guid? DependsOnProgramId { get; set; }

    /// <summary>
    /// Backward-compatibility alias forwarding to <see cref="DependsOnItemId"/>.
    /// </summary>
    [NotMapped]
    public Guid? TargetTaskId
    {
        get => DependsOnItemId;
        set => DependsOnItemId = value;
    }

    // =========================================================================
    // Decoupled Soft-ID Snapshot (Zero-Trust RBAC & Performance Boundary)
    // Maintained by the background Dependency Engine / SystemDaemon.
    // =========================================================================

    /// <summary>
    /// Cached headline title of Side A, enabling Side B viewers to inspect the dependency without violating Side A's project RBAC.
    /// </summary>
    public string? DependsOnSnapshotTitle { get; set; }

    /// <summary>
    /// Cached status label of Side A (e.g. Open, Delayed, AtRisk, Completed, Closed).
    /// </summary>
    public string? DependsOnSnapshotStatus { get; set; }

    /// <summary>
    /// Cached target resolution/completion deadline of Side A.
    /// </summary>
    public DateTime? DependsOnSnapshotDueDate { get; set; }

    /// <summary>
    /// Indicates whether Side A's due date or end date creates a schedule slippage conflict with Side B's timeline.
    /// </summary>
    public bool IsScheduleConflict { get; set; }

    // =========================================================================
    // Dependency & RAID Metadata
    // =========================================================================

    /// <summary>
    /// Semantic nature of the relationship (Blocks, BlockedBy, RelatesTo).
    /// </summary>
    public TaskDependencyType DependencyType { get; set; } = TaskDependencyType.Blocks;

    /// <summary>
    /// Optional foreign key to a project or program milestone.
    /// </summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Unmapped convenience alias forwarding to <see cref="BaseTaskEntity.Description"/> to avoid duplicate storage.
    /// </summary>
    [NotMapped]
    public string? ImpactDetails
    {
        get => Description;
        set => Description = value;
    }

    /// <summary>
    /// Unmapped convenience alias mapping to and from <see cref="BaseTaskEntity.Priority"/> to avoid duplicate storage.
    /// </summary>
    [NotMapped]
    public DependencyCriticality Criticality
    {
        get => Priority switch
        {
            TaskPriority.Low => DependencyCriticality.Low,
            TaskPriority.Medium => DependencyCriticality.Medium,
            TaskPriority.High => DependencyCriticality.High,
            TaskPriority.Urgent => DependencyCriticality.Critical,
            _ => DependencyCriticality.Medium
        };
        set => Priority = value switch
        {
            DependencyCriticality.Low => TaskPriority.Low,
            DependencyCriticality.Medium => TaskPriority.Medium,
            DependencyCriticality.High => TaskPriority.High,
            DependencyCriticality.Critical => TaskPriority.Urgent,
            _ => TaskPriority.Medium
        };
    }

    /// <summary>
    /// Explicit target deadline by which this dependency must be resolved.
    /// Synchronized with BaseTaskEntity.DueDate.
    /// </summary>
    [NotMapped]
    public DateTime ResolveDueDate
    {
        get => DueDate;
        set => DueDate = value;
    }

    /// <summary>
    /// Indicates whether the blocker contract has been completed.
    /// Derived from status.
    /// </summary>
    [NotMapped]
    public bool IsResolved => Status is TaskStatus.Closed or TaskStatus.Cancelled;
}
