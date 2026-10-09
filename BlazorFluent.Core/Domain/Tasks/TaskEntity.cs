using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Standard work item / task.
/// Inherits from <see cref="BaseTaskEntity"/>.
/// Can have one or more <see cref="SubTaskEntity"/> children.
/// </summary>
public class TaskEntity : BaseTaskEntity, IProjectScopedEntity
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
}
