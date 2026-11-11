namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// A task acting as a sequential step inside a governing workflow pipeline.
/// Mapped via EF Core TPC to table 'tasks.WorkflowTasks'.
/// WorkflowId and WorkflowStepOrder are mandatory and not nullable.
/// </summary>
public class WorkflowTaskEntity : TaskEntity
{
    /// <summary>
    /// Foreign key referencing the governing workflow pipeline.
    /// </summary>
    public required Guid WorkflowId { get; set; }

    /// <summary>
    /// 1-based sequential execution index within the workflow pipeline.
    /// </summary>
    public required int WorkflowStepOrder { get; set; }

    /// <summary>
    /// Navigation property to the governing workflow pipeline.
    /// </summary>
    public WorkflowEntity? Workflow { get; set; }
}
