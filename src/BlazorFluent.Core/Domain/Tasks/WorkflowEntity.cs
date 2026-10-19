using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Enterprise workflow container entity orchestrating sequential, stage-gated step completion.
/// Can exist at Tenant, Program, or Project organizational level.
/// Governed by a constant Workflow Owner who maintains overarching delivery accountability.
/// Governs an ordered collection of sequential steps (<see cref="TaskEntity"/>).
/// Inherits from <see cref="BaseTaskEntity"/>.
/// </summary>
public class WorkflowEntity : BaseTaskEntity
{
    /// <summary>
    /// Email of the single, constant workflow owner who retains operational accountability
    /// and follows up across all steps to avoid delays.
    /// </summary>
    public required string WorkflowOwnerEmail { get; set; }

    /// <summary>
    /// Unique identifier of the currently active step (TaskEntity.Id).
    /// Null if the workflow is completed, cancelled, or not yet started.
    /// </summary>
    public Guid? CurrentStepId { get; set; }

    /// <summary>
    /// Ordered collection of sequential steps governed by this workflow.
    /// </summary>
    public ICollection<TaskEntity> Steps { get; set; } = new List<TaskEntity>();
}
