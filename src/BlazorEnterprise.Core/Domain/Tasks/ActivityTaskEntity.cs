namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// A developer activity or work task representing any activity, review, maintenance, testing,
/// or work item that needs completion with associated action items.
/// Mapped via EF Core TPC to table 'tasks.ActivityTasks'.
/// </summary>
public class ActivityTaskEntity : TaskEntity
{
}
