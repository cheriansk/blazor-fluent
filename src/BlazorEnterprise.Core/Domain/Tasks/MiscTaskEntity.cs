namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// A standalone, ad-hoc, or backlog task not tied to a sprint iteration or workflow pipeline.
/// Used for tenant-level tasks, ad-hoc directives (e.g. from lead to developer), and project backlog items.
/// Mapped via EF Core TPC to table 'tasks.MiscTasks'.
/// </summary>
public class MiscTaskEntity : TaskEntity
{
}
