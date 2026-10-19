using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Represents a discrete, sequential stage or gate within an enterprise <see cref="WorkflowTaskEntity"/>.
/// Governed by an exact execution order and assigned to exactly one owner.
/// Inherits from <see cref="BaseTaskEntity"/>.
/// </summary>
public class WorkflowStepEntity : BaseTaskEntity
{
    /// <summary>
    /// Foreign key referencing the parent <see cref="WorkflowTaskEntity"/>.
    /// </summary>
    public required Guid WorkflowTaskId { get; set; }

    /// <summary>
    /// 1-based sequential execution index (Step 1, Step 2, Step 3...).
    /// </summary>
    public required int StepOrder { get; set; }

    /// <summary>
    /// Discrete lifecycle status of this step.
    /// </summary>
    public required WorkflowStepStatus StepStatus { get; set; }

    /// <summary>
    /// Email of the verified individual responsible for executing this specific step.
    /// </summary>
    public required string StepOwnerEmail { get; set; }

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
