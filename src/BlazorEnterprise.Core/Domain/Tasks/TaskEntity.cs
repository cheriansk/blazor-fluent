using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Sprints;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Standard work item / task.
/// Inherits from <see cref="BaseTaskEntity"/>.
/// Can act as a standalone task or as a sequential step in a <see cref="WorkflowEntity"/>.
/// Can have one or more <see cref="SubTaskEntity"/> children delegated to team members.
/// </summary>
public partial class TaskEntity : BaseTaskEntity, IProjectScopedEntity, IMilestoneLinkedEntity, IUnversionedEntity
{
    /// <summary>
    /// ID of the project to which this task belongs (when Level is Project).
    /// </summary>
    public new Guid ProjectId
    {
        get => base.ProjectId ?? Guid.Empty;
        set => base.ProjectId = value == Guid.Empty ? null : value;
    }

    /// <summary>
    /// Planned / estimated effort hours for the task.
    /// Not present or available on SubTaskEntity.
    /// </summary>
    public decimal? EstimatedHours { get; set; }

    /// <summary>
    /// Optional foreign key to a project milestone. Null indicates an ad-hoc task.
    /// </summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Optional foreign key to the assigned sprint iteration. Null indicates backlog / unassigned.
    /// A task can be assigned to at most 1 sprint at a time.
    /// </summary>
    public Guid? SprintId { get; set; }

    /// <summary>
    /// Assigned sprint iteration.
    /// </summary>
    public SprintEntity? Sprint { get; set; }

    /// <summary>
    /// Optional foreign key referencing the governing workflow pipeline (null for standalone tasks).
    /// </summary>
    public Guid? WorkflowId { get; set; }

    /// <summary>
    /// 1-based sequential execution index within the workflow (null for standalone tasks).
    /// </summary>
    public int? WorkflowStepOrder { get; set; }

    /// <summary>
    /// Governing workflow pipeline.
    /// </summary>
    public WorkflowEntity? Workflow { get; set; }

    /// <summary>
    /// Collection of child subtasks directly governed by this task.
    /// </summary>
    public ICollection<SubTaskEntity> SubTasks { get; set; } = new List<SubTaskEntity>();

    /// <summary>
    /// Discussion comments thread.
    /// </summary>
    public ICollection<UserTaskCommentEntity> Comments { get; set; } = new List<UserTaskCommentEntity>();
}
