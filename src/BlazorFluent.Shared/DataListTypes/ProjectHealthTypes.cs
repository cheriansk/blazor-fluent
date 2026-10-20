using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Traffic-light health evaluation rating for projects and milestones.
/// </summary>
public enum ProjectHealthRatingEnum
{
    [Display(Name = "On Track", Description = "Deliverables progressing on schedule with zero overdue blockers.")]
    OnTrack = 1,

    [Display(Name = "At Risk", Description = "Minor schedule variance, active blockers approaching deadline, or delayed predecessor milestones.")]
    AtRisk = 2,

    [Display(Name = "Critical", Description = "Overdue blockers, milestone end date breached, or significant task delay.")]
    Critical = 3
}

/// <summary>
/// Relational classification between two project tasks.
/// </summary>
public enum TaskDependencyTypeEnum
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
    public ProjectHealthRatingEnum Health { get; set; } = ProjectHealthRatingEnum.OnTrack;
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
    public CalendarEntryStatusEnum Status { get; set; }
    public CalendarEntryTypeEnum EntryType { get; set; } = CalendarEntryTypeEnum.Milestone;
    public ProjectHealthRatingEnum Health { get; set; } = ProjectHealthRatingEnum.OnTrack;
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
/// DTO representing a dependency item across tasks, calendar gates, or manual dependencies.
/// </summary>
public class TaskDependencyItemDto
{
    public Guid DependencyId { get; set; }

    // --- Side B: Dependent / Successor ---
    public DependencyNodeTypeEnum DependentType { get; set; } = DependencyNodeTypeEnum.TaskItem;
    public Guid? DependentItemId { get; set; }
    public TaskLevelEnum DependentLevel { get; set; } = TaskLevelEnum.Project;
    public Guid? ProjectId { get; set; }
    public Guid? ProgramId { get; set; }

    // Backward-compat forwarder for legacy TaskId
    public Guid TaskId
    {
        get => DependentItemId ?? Guid.Empty;
        set => DependentItemId = value;
    }

    // --- Side A: Prerequisite / Predecessor ---
    public DependencyNodeTypeEnum DependsOnType { get; set; } = DependencyNodeTypeEnum.TaskItem;
    public Guid? DependsOnItemId { get; set; }
    public TaskLevelEnum DependsOnLevel { get; set; } = TaskLevelEnum.Project;
    public Guid? DependsOnProjectId { get; set; }
    public Guid? DependsOnProgramId { get; set; }

    // Backward-compat forwarder for legacy DependsOnTaskId
    public Guid DependsOnTaskId
    {
        get => DependsOnItemId ?? Guid.Empty;
        set => DependsOnItemId = value;
    }

    public string DependsOnTitle { get; set; } = string.Empty;
    public string DependsOnTaskTitle
    {
        get => DependsOnTitle;
        set => DependsOnTitle = value;
    }

    public TaskStatusEnum DependsOnTaskStatus { get; set; } = TaskStatusEnum.Open;
    public string DependsOnStatus { get; set; } = "Open";
    public string DependsOnAssigneeEmails { get; set; } = string.Empty;
    public DateTime? DependsOnDueDate { get; set; }

    // --- Dependency & RAID Metadata ---
    public string Title { get; set; } = string.Empty;
    public string ImpactDetails { get; set; } = string.Empty;
    public TaskDependencyTypeEnum DependencyType { get; set; } = TaskDependencyTypeEnum.Blocks;
    public TaskPriorityEnum Criticality { get; set; } = TaskPriorityEnum.Medium;
    public TaskStatusEnum Status { get; set; } = TaskStatusEnum.Open;
    public DateTime? ResolveByUtc { get; set; }
    public bool IsScheduleConflict { get; set; }

    public bool IsResolved => Status is TaskStatusEnum.Closed or TaskStatusEnum.Cancelled ||
                              DependsOnTaskStatus is TaskStatusEnum.Closed or TaskStatusEnum.Cancelled ||
                              DependsOnStatus is "Completed" or "Closed";
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
    public CalendarEntryStatusEnum DependsOnStatus { get; set; }
    public DateTime? DependsOnEndDate { get; set; }
    public bool IsCompleted => DependsOnStatus == CalendarEntryStatusEnum.Completed;
}