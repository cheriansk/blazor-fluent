using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Standard work item / task.
/// Inherits from <see cref="BaseTaskEntity"/>.
/// Can act as a standalone task or as a sequential step in a <see cref="WorkflowTaskEntity"/>.
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
    /// Collection of child subtasks directly governed by this task.
    /// </summary>
    public ICollection<SubTaskEntity> SubTasks { get; set; } = new List<SubTaskEntity>();

    /// <summary>
    /// Collection of outgoing dependency links where this task is the source.
    /// </summary>
    public ICollection<TaskLinkEntity> OutgoingLinks { get; set; } = new List<TaskLinkEntity>();

    /// <summary>
    /// Collection of incoming dependency links where this task is the target.
    /// </summary>
    public ICollection<TaskLinkEntity> IncomingLinks { get; set; } = new List<TaskLinkEntity>();

    /// <summary>
    /// Discussion comments thread.
    /// </summary>
    public ICollection<UserTaskCommentEntity> Comments { get; set; } = new List<UserTaskCommentEntity>();

    // --- Sequential Workflow Step Integration ---

    /// <summary>
    /// Optional foreign key to a governing <see cref="WorkflowTaskEntity"/> when this task acts as a sequential workflow step.
    /// </summary>
    public Guid? WorkflowTaskId { get; set; }

    /// <summary>
    /// 1-based execution order index within the parent workflow (Step 1, Step 2, Step 3...).
    /// </summary>
    public int? WorkflowStepOrder { get; set; }

    /// <summary>
    /// Discrete lifecycle status of this step within the sequential workflow.
    /// </summary>
    public WorkflowStepStatus? WorkflowStepStatus { get; set; }

    /// <summary>
    /// Audit notes or delivery sign-off comments recorded by the step owner upon completion.
    /// </summary>
    public string? WorkflowStepCompletionNotes { get; set; }

    /// <summary>
    /// Timestamp when this step transitioned to Active in the workflow.
    /// </summary>
    public DateTime? WorkflowStepActivatedAtUtc { get; set; }

    /// <summary>
    /// Timestamp when this step was marked Completed in the workflow.
    /// </summary>
    public DateTime? WorkflowStepCompletedAtUtc { get; set; }

    /// <summary>
    /// Navigation property to the parent workflow.
    /// </summary>
    public WorkflowTaskEntity? WorkflowTask { get; set; }
}
