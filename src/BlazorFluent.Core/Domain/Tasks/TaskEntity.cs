using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Standard work item / task.
/// Inherits from <see cref="BaseTaskEntity"/>.
/// Can act as a standalone task or as a sequential step in a <see cref="WorkflowEntity"/>.
/// Can have one or more <see cref="SubTaskEntity"/> children delegated to team members.
/// </summary>
public partial class TaskEntity : BaseTaskEntity, IProjectScopedEntity, IMilestoneLinkedEntity
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
    /// Optional foreign key to a project milestone. Null indicates an ad-hoc task.
    /// </summary>
    public Guid? MilestoneId { get; set; }

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
