namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// Contract for domain entities (tasks, RAID items) that can be optionally associated with a Milestone.
/// Subtasks do not implement this directly as their milestone association is inherited from their parent task.
/// </summary>
public interface IMilestoneLinkedEntity
{
    /// <summary>
    /// Optional foreign key referencing the governing milestone (e.g. Program or Project Calendar entry of type Milestone).
    /// Null indicates an ad-hoc or unassigned item.
    /// </summary>
    Guid? MilestoneId { get; set; }
}
