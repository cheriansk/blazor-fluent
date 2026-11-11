using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Abstract base class for executable tasks.
/// Concrete subtypes use EF Core TPC mapping: SprintTaskEntity, WorkflowTaskEntity, MiscTaskEntity.
/// Inherits from <see cref="BaseTaskEntity"/>.
/// </summary>
public abstract partial class TaskEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    /// <summary>
    /// Planned / estimated effort hours for the task.
    /// </summary>
    public decimal? EstimatedHours { get; set; }

    /// <summary>
    /// Optional milestone association. Hydrated via MilestoneRelationEntity.
    /// </summary>
    [NotMapped]
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Collection of child subtasks directly governed by this task.
    /// </summary>
    public ICollection<SubTaskEntity> SubTasks { get; set; } = new List<SubTaskEntity>();

    /// <summary>
    /// Discussion comments thread.
    /// </summary>
    public ICollection<UserTaskCommentEntity> Comments { get; set; } = new List<UserTaskCommentEntity>();
}
