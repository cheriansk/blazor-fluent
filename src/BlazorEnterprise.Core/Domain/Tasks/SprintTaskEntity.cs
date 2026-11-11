using BlazorEnterprise.Core.Domain.Sprints;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// A task assigned directly to a sprint iteration.
/// Mapped via EF Core TPC to table 'tasks.SprintTasks'.
/// SprintId is mandatory and not nullable.
/// </summary>
public class SprintTaskEntity : TaskEntity
{
    /// <summary>
    /// Foreign key referencing the assigned sprint iteration.
    /// </summary>
    public required Guid SprintId { get; set; }

    /// <summary>
    /// Navigation property to the assigned sprint iteration.
    /// </summary>
    public SprintEntity? Sprint { get; set; }
}
