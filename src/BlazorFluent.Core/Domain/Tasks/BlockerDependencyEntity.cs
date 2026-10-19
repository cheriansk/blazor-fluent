using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Concrete, accountable blocker dependency contract ("D" of RAID).
/// Subtype of <see cref="BaseTaskEntity"/> mapped to table 'blocker_dependencies'.
/// Automatically created when a task link with TaskDependencyType.BlockedBy is established.
/// State integrity: Automatically closed when prerequisite TargetTask completes;
/// automatically reopened when prerequisite TargetTask is reopened.
/// Implements <see cref="IMilestoneLinkedEntity"/> for optional milestone association.
/// </summary>
public class BlockerDependencyEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    /// <summary>
    /// ID of the dependent task waiting / blocked (e.g. Task A).
    /// </summary>
    public required Guid SourceTaskId { get; set; }

    /// <summary>
    /// ID of the prerequisite task that is blocking (e.g. Task B).
    /// </summary>
    public required Guid TargetTaskId { get; set; }

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

    // --- Navigation Properties ---
    public TaskEntity? SourceTask { get; set; }
    public TaskEntity? TargetTask { get; set; }
}
