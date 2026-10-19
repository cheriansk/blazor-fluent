using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Traffic-light health evaluation rating for projects and milestones.
/// </summary>
public enum ProjectHealthRating
{
    [Display(Name = "On Track", Description = "Deliverables progressing on schedule with zero overdue blockers.")]
    OnTrack = 1,

    [Display(Name = "At Risk", Description = "Minor schedule variance, active blockers approaching deadline, or delayed predecessor milestones.")]
    AtRisk = 2,

    [Display(Name = "Critical", Description = "Overdue blockers, milestone end date breached, or significant task delay.")]
    Critical = 3
}

/// <summary>
/// Lifecycle status of a project milestone.
/// </summary>
public enum MilestoneStatus
{
    [Display(Name = "Planned", Description = "Milestone defined, pending kickoff.")]
    Planned = 1,

    [Display(Name = "Active", Description = "Milestone currently underway.")]
    Active = 2,

    [Display(Name = "Completed", Description = "All deliverables achieved and signed off.")]
    Completed = 3,

    [Display(Name = "At Risk", Description = "Milestone facing schedule slippage or unresolved blockers.")]
    AtRisk = 4,

    [Display(Name = "Cancelled", Description = "Milestone aborted or scope descoped.")]
    Cancelled = 5
}

/// <summary>
/// Relational classification between two project tasks.
/// </summary>
public enum TaskDependencyType
{
    /// <summary>
    /// Computed / screen display view only. Not available for selection in create dropdown.
    /// When Task B is BlockedBy Task A, Task A displays that it Blocks Task B.
    /// </summary>
    [Display(Name = "Blocks", Description = "This item blocks a dependent task")]
    Blocks = 1,

    /// <summary>
    /// Primary selectable link type prompting for resolve SLA, criticality, and impact details.
    /// </summary>
    [Display(Name = "Blocked By", Description = "This item is waiting on a prerequisite deliverable")]
    BlockedBy = 2,

    /// <summary>
    /// Informational association between items.
    /// </summary>
    [Display(Name = "Relates To", Description = "General cross-reference")]
    RelatesTo = 3,

    /// <summary>
    /// Parent-child association assigned automatically when a subtask is added to a task.
    /// </summary>
    [Display(Name = "Parent", Description = "Parent task governing a subtask")]
    Parent = 4
}

/// <summary>
/// Summary DTO holding real-time project-level health metrics and root-cause analysis.
/// </summary>
public class ProjectHealthSummaryDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectHealthRating Health { get; set; } = ProjectHealthRating.OnTrack;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int OpenTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int ActiveBlockersCount { get; set; }
    public int TotalMilestones { get; set; }
    public int CompletedMilestones { get; set; }
    public double ProgressPercentage => TotalTasks == 0 ? 0 : Math.Round((double)CompletedTasks / TotalTasks * 100, 1);
    public List<string> RiskReasons { get; set; } = new();
}

/// <summary>
/// Summary DTO holding real-time milestone health metrics and dependency status.
/// </summary>
public class MilestoneHealthSummaryDto
{
    public Guid MilestoneId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public MilestoneStatus Status { get; set; }
    public ProjectHealthRating Health { get; set; } = ProjectHealthRating.OnTrack;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int ActiveBlockersCount { get; set; }
    public int DaysRemaining { get; set; }
    public bool HasOverduePredecessors { get; set; }
    public double ProgressPercentage => TotalTasks == 0 ? 0 : Math.Round((double)CompletedTasks / TotalTasks * 100, 1);
    public List<string> PredecessorMilestoneNames { get; set; } = new();
    public List<string> RiskReasons { get; set; } = new();
}

/// <summary>
/// DTO representing a task dependency link.
/// </summary>
public class TaskDependencyItemDto
{
    public Guid DependencyId { get; set; }
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
    public string DependsOnTaskTitle { get; set; } = string.Empty;
    public TaskStatus DependsOnTaskStatus { get; set; }
    public string DependsOnAssigneeEmails { get; set; } = string.Empty;
    public DateTime? DependsOnDueDate { get; set; }
    public TaskDependencyType DependencyType { get; set; }
    public DateTime? ResolveByUtc { get; set; }
    public bool IsResolved => DependsOnTaskStatus is TaskStatus.Closed or TaskStatus.Cancelled;
    public bool IsOverdue => !IsResolved && ResolveByUtc.HasValue && ResolveByUtc.Value.Date < DateTime.UtcNow.Date;
}

/// <summary>
/// DTO representing a milestone dependency link.
/// </summary>
public class MilestoneDependencyItemDto
{
    public Guid Id { get; set; }
    public Guid MilestoneId { get; set; }
    public Guid DependsOnMilestoneId { get; set; }
    public string DependsOnMilestoneName { get; set; } = string.Empty;
    public MilestoneStatus DependsOnStatus { get; set; }
    public DateTime? DependsOnEndDate { get; set; }
    public bool IsCompleted => DependsOnStatus == MilestoneStatus.Completed;
}
