using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Explicit task dependency link tracking blockers and resolution deadlines.
/// Enforces tenant and project isolation.
/// </summary>
public class TaskDependencyEntity : TenantAuditableEntity, IProjectScopedEntity
{
    /// <summary>
    /// ID of the project owning both tasks.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The dependent task that is blocked or related.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// The prerequisite task that is blocking or prerequisite.
    /// </summary>
    public Guid DependsOnTaskId { get; set; }

    /// <summary>
    /// Classification of dependency (Blocks, BlockedBy, RelatesTo).
    /// </summary>
    public TaskDependencyType DependencyType { get; set; } = TaskDependencyType.Blocks;

    /// <summary>
    /// Target deadline by which this blocking dependency must be resolved (UTC).
    /// Used by Cadence notification engine to proactively alert blocker assignees.
    /// </summary>
    public DateTime? ResolveByUtc { get; set; }

    /// <summary>
    /// Contextual explanation or requirement notes.
    /// </summary>
    public string? Notes { get; set; }
}
