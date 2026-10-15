using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Represents a discrete, sequential stage or gate within an enterprise <see cref="WorkflowTaskEntity"/>.
/// Governed by an exact execution order and assigned to exactly one owner.
/// Inherits from <see cref="BaseTaskEntity"/>.
/// </summary>
public class WorkflowStepEntity : BaseTaskEntity
{
    public WorkflowStepEntity()
    {
        Level = TaskLevel.Project;
    }

    /// <summary>
    /// Foreign key referencing the parent <see cref="WorkflowTaskEntity"/>.
    /// </summary>
    public Guid WorkflowTaskId { get; set; }

    /// <summary>
    /// 1-based sequential execution index (Step 1, Step 2, Step 3...).
    /// </summary>
    public int StepOrder { get; set; } = 1;

    /// <summary>
    /// Discrete lifecycle status of this step.
    /// </summary>
    public WorkflowStepStatus StepStatus { get; set; } = WorkflowStepStatus.Pending;

    /// <summary>
    /// Email of the verified individual responsible for executing this specific step.
    /// </summary>
    public string StepOwnerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this step transitioned to Active.
    /// </summary>
    public DateTime? ActivatedAtUtc { get; set; }

    /// <summary>
    /// Timestamp when this step was marked Completed.
    /// </summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Audit notes or delivery summary recorded by the step owner upon completion.
    /// </summary>
    public string? CompletionNotes { get; set; }

    // --- Navigation Properties ---
    public WorkflowTaskEntity? WorkflowTask { get; set; }
}
