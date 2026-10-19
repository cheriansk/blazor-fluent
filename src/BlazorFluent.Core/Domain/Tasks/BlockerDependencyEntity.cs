using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Concrete, accountable blocker dependency contract ("D" of RAID).
/// Subtype of <see cref="BaseTaskEntity"/> mapped to table 'blocker_dependencies'.
/// Automatically created when a task link with TaskDependencyType.BlockedBy is established.
/// State integrity: Automatically closed when prerequisite TargetTask completes;
/// automatically reopened when prerequisite TargetTask is reopened.
/// </summary>
public class BlockerDependencyEntity : BaseTaskEntity
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
    /// Multi-paragraph or bulleted explanation of impacts, blast radius, and consequences if the blocker is not resolved on time.
    /// </summary>
    public string? ImpactDetails { get; set; }

    /// <summary>
    /// Criticality / urgency rating of this dependency contract.
    /// </summary>
    public required DependencyCriticality Criticality { get; set; }

    /// <summary>
    /// Explicit target deadline by which this dependency must be resolved.
    /// Synchronized with BaseTaskEntity.DueDate.
    /// </summary>
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
