using BlazorFluent.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Organizational scoping level for a work item or calendar event.
/// </summary>
[ClassificationEnum]
public enum TaskLevelEnum
{
    [Display(Name = "Tenant", Description = "Executive tenant-wide governance item")]
    Tenant = 1,

    [Display(Name = "Program", Description = "Multi-project strategic program item")]
    Program = 2,

    [Display(Name = "Project", Description = "Team project delivery item")]
    Project = 3,

    [Display(Name = "User", Description = "Personal workload item")]
    User = 4
}

/// <summary>
/// Execution and SLA priority rating for a task or work item.
/// </summary>
[ClassificationEnum]
public enum TaskPriorityEnum
{
    [Display(Name = "Low", Description = "Routine task, low business impact")]
    Low = 1,

    [Display(Name = "Medium", Description = "Standard operational priority")]
    Medium = 2,

    [Display(Name = "High", Description = "Critical path item, urgent timeline")]
    High = 3,

    [Display(Name = "Urgent", Description = "Blocker requiring immediate intervention")]
    Urgent = 4
}

/// <summary>
/// Lifecycle status of any work item inheriting from BaseTaskEntity.
/// Multi-category membership allows statuses to be partitioned across Tasks, SubTasks, and Risks.
/// </summary>
[LifecycleEnum]
public enum TaskStatusEnum
{
    [Display(Name = "Open", Description = "Ready for work, awaiting triage")]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Task)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.SubTask)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Risk)]
    Open = 1,

    [Display(Name = "In Progress", Description = "Work actively underway")]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Task)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.SubTask)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Risk)]
    InProgress = 2,

    [Display(Name = "Closed", Description = "Deliverable completed and verified")]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Task)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.SubTask)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Risk)]
    Closed = 3,

    [Display(Name = "Cancelled", Description = "Work item descoped or abandoned")]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Task)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.SubTask)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Risk)]
    Cancelled = 4,

    [Display(Name = "Paused", Description = "Pipeline paused, awaiting administrator decision")]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Task)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.SubTask)]
    Paused = 5,

    [Display(Name = "Pending", Description = "Awaiting preceding workflow step completion")]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.Task)]
    [DataListCategory(TaskStatusEnumDefinitions.Categories.SubTask)]
    Pending = 6
}

public static class TaskStatusEnumDefinitions
{
    public static class Categories
    {
        [Display(Name = "Project Task", Description = "Standard deliverables and work items")]
        public const string Task = "TASK";

        [Display(Name = "Sub-Task", Description = "Discrete child work item")]
        public const string SubTask = "SUBTASK";

        [Display(Name = "Enterprise Risk", Description = "Risk deliverable or threat item")]
        public const string Risk = "RISK";
    }

    /// <summary>
    /// Category-scoped state transition rules.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<TaskStatusEnum, IReadOnlySet<TaskStatusEnum>>> AllowedTransitions =
        new Dictionary<string, IReadOnlyDictionary<TaskStatusEnum, IReadOnlySet<TaskStatusEnum>>>(StringComparer.OrdinalIgnoreCase)
        {
            [Categories.Task] = new Dictionary<TaskStatusEnum, IReadOnlySet<TaskStatusEnum>>
            {
                [TaskStatusEnum.Open] = new HashSet<TaskStatusEnum> { TaskStatusEnum.InProgress, TaskStatusEnum.Paused, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.InProgress] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Paused, TaskStatusEnum.Closed, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.Paused] = new HashSet<TaskStatusEnum> { TaskStatusEnum.InProgress, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.Pending] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Open, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.Closed] = new HashSet<TaskStatusEnum> { TaskStatusEnum.InProgress },
                [TaskStatusEnum.Cancelled] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Open }
            },

            [Categories.SubTask] = new Dictionary<TaskStatusEnum, IReadOnlySet<TaskStatusEnum>>
            {
                [TaskStatusEnum.Open] = new HashSet<TaskStatusEnum> { TaskStatusEnum.InProgress, TaskStatusEnum.Paused, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.InProgress] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Closed, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.Paused] = new HashSet<TaskStatusEnum> { TaskStatusEnum.InProgress, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.Pending] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Open, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.Closed] = new HashSet<TaskStatusEnum> { TaskStatusEnum.InProgress },
                [TaskStatusEnum.Cancelled] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Open }
            },

            [Categories.Risk] = new Dictionary<TaskStatusEnum, IReadOnlySet<TaskStatusEnum>>
            {
                [TaskStatusEnum.Open] = new HashSet<TaskStatusEnum> { TaskStatusEnum.InProgress, TaskStatusEnum.Closed, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.InProgress] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Closed, TaskStatusEnum.Cancelled },
                [TaskStatusEnum.Closed] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Open },
                [TaskStatusEnum.Cancelled] = new HashSet<TaskStatusEnum> { TaskStatusEnum.Open }
            }
        };
}

/// <summary>
/// Discriminator classification for work items.
/// </summary>
[ClassificationEnum]
public enum TaskItemTypeEnum
{
    Task = 1,
    SubTask = 2,
    Risk = 3,
    Assumption = 4,
    Issue = 5,
    BlockerDependency = 6,
    Workflow = 7
}

/// <summary>
/// Impact severity rating for formal enterprise risks.
/// </summary>
[ClassificationEnum]
public enum RiskImpactEnum
{
    [Display(Name = "Low", Description = "Minor inconvenience, negligible cost")]
    Low = 1,

    [Display(Name = "Medium", Description = "Manageable schedule or budget variance")]
    Medium = 2,

    [Display(Name = "High", Description = "Significant timeline slip or budget impact")]
    High = 3,

    [Display(Name = "Critical", Description = "Severe existential threat to program success")]
    Critical = 4
}

/// <summary>
/// Probability of risk realization.
/// </summary>
[ClassificationEnum]
public enum RiskProbabilityEnum
{
    [Display(Name = "Low", Description = "Unlikely to occur (< 25%)")]
    Low = 1,

    [Display(Name = "Medium", Description = "Possible to occur (25% - 75%)")]
    Medium = 2,

    [Display(Name = "High", Description = "High probability of realization (> 75%)")]
    High = 3
}

/// <summary>
/// Lifecycle governance status for an enterprise risk.
/// </summary>
[LifecycleEnum]
public enum RiskStatusEnum
{
    [Display(Name = "Identified", Description = "Risk logged, awaiting mitigation planning")]
    Identified = 1,

    [Display(Name = "Mitigating", Description = "Active mitigation actions underway")]
    Mitigating = 2,

    [Display(Name = "Monitored", Description = "Residual risk being monitored")]
    Monitored = 3,

    [Display(Name = "Realized", Description = "Risk event occurred, transitioned to Issue")]
    Realized = 4,

    [Display(Name = "Closed", Description = "Risk eliminated or expired")]
    Closed = 5
}

public static class RiskStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<RiskStatusEnum, IReadOnlySet<RiskStatusEnum>> AllowedTransitions =
        new Dictionary<RiskStatusEnum, IReadOnlySet<RiskStatusEnum>>
        {
            [RiskStatusEnum.Identified] = new HashSet<RiskStatusEnum> { RiskStatusEnum.Mitigating, RiskStatusEnum.Closed },
            [RiskStatusEnum.Mitigating] = new HashSet<RiskStatusEnum> { RiskStatusEnum.Monitored, RiskStatusEnum.Realized, RiskStatusEnum.Closed },
            [RiskStatusEnum.Monitored] = new HashSet<RiskStatusEnum> { RiskStatusEnum.Mitigating, RiskStatusEnum.Realized, RiskStatusEnum.Closed },
            [RiskStatusEnum.Realized] = new HashSet<RiskStatusEnum> { RiskStatusEnum.Closed },
            [RiskStatusEnum.Closed] = new HashSet<RiskStatusEnum> { RiskStatusEnum.Identified }
        };
}

/// <summary>
/// Validation status for an enterprise assumption (A of RAID).
/// </summary>
[LifecycleEnum]
public enum AssumptionValidationStatusEnum
{
    [Display(Name = "Unconfirmed", Description = "Assumption made, requires validation")]
    Unconfirmed = 1,

    [Display(Name = "Validated", Description = "Assumption verified as true")]
    Validated = 2,

    [Display(Name = "Invalidated", Description = "Assumption proven false, requires replanning")]
    Invalidated = 3
}

public static class AssumptionValidationStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<AssumptionValidationStatusEnum, IReadOnlySet<AssumptionValidationStatusEnum>> AllowedTransitions =
        new Dictionary<AssumptionValidationStatusEnum, IReadOnlySet<AssumptionValidationStatusEnum>>
        {
            [AssumptionValidationStatusEnum.Unconfirmed] = new HashSet<AssumptionValidationStatusEnum> { AssumptionValidationStatusEnum.Validated, AssumptionValidationStatusEnum.Invalidated },
            [AssumptionValidationStatusEnum.Validated] = new HashSet<AssumptionValidationStatusEnum> { AssumptionValidationStatusEnum.Invalidated },
            [AssumptionValidationStatusEnum.Invalidated] = new HashSet<AssumptionValidationStatusEnum> { AssumptionValidationStatusEnum.Unconfirmed }
        };
}

/// <summary>
/// Severity rating for an active issue (I of RAID).
/// </summary>
[ClassificationEnum]
public enum IssueSeverityEnum
{
    [Display(Name = "Low", Description = "Minor issue, workaround readily available")]
    Low = 1,

    [Display(Name = "Medium", Description = "Moderate issue impacting velocity")]
    Medium = 2,

    [Display(Name = "High", Description = "Major impediment blocking key deliverables")]
    High = 3,

    [Display(Name = "Critical", Description = "Production outage or delivery showstopper")]
    Critical = 4
}

/// <summary>
/// Criticality rating for a blocker dependency contract.
/// </summary>
[ClassificationEnum]
public enum DependencyCriticalityEnum
{
    [Display(Name = "Low", Description = "Minor convenience dependency")]
    Low = 1,

    [Display(Name = "Medium", Description = "Standard dependency with schedule buffer")]
    Medium = 2,

    [Display(Name = "High", Description = "Tight dependency on the near critical path")]
    High = 3,

    [Display(Name = "Critical", Description = "Hard blocker directly halting next milestone")]
    Critical = 4
}

/// <summary>
/// Category of a calendar entry.
/// </summary>
[ClassificationEnum]
public enum CalendarEntryTypeEnum
{
    [Display(Name = "Planning Session", Description = "Sprint or roadmap planning")]
    Planning = 1,

    [Display(Name = "Milestone Review", Description = "Executive milestone check")]
    Milestone = 2,

    [Display(Name = "Release", Description = "Target deployment or production release")]
    Release = 3,

    [Display(Name = "Steering Review", Description = "Governance or audit review")]
    Review = 4,

    [Display(Name = "Sprint Ceremony", Description = "Daily standup, demo, or retro")]
    SprintEvent = 5,

    [Display(Name = "Working Session", Description = "Team architecture or delivery workshop")]
    WorkingSession = 6,

    [Display(Name = "Time Off", Description = "Planned absence or non-working day")]
    TimeOff = 7,

    [Display(Name = "General", Description = "General scheduled event")]
    General = 8,

    [Display(Name = "Sprint", Description = "Sprint start and end date cadence")]
    Sprint = 9,

    [Display(Name = "Deadline", Description = "Target completion deadline or code freeze")]
    Deadline = 10,

    [Display(Name = "Vendor Delivery", Description = "Inbound or outbound vendor/partner delivery gate")]
    VendorDelivery = 11,

    [Display(Name = "SIT Window", Description = "System Integration Testing execution and sign-off")]
    SitWindow = 12,

    [Display(Name = "UAT Window", Description = "User Acceptance Testing execution and business sign-off")]
    UatWindow = 13,

    [Display(Name = "Go-Live Preparation", Description = "Cutover dry run, dress rehearsal, or go-live prep")]
    GoLivePrep = 14
}

/// <summary>
/// Status of a calendar entry.
/// </summary>
[LifecycleEnum]
public enum CalendarEntryStatusEnum
{
    Scheduled = 1,
    Confirmed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    AtRisk = 6,
    Delayed = 7
}

public static class CalendarEntryStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<CalendarEntryStatusEnum, IReadOnlySet<CalendarEntryStatusEnum>> AllowedTransitions =
        new Dictionary<CalendarEntryStatusEnum, IReadOnlySet<CalendarEntryStatusEnum>>
        {
            [CalendarEntryStatusEnum.Scheduled] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.Confirmed, CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.Confirmed] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.AtRisk, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.InProgress] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.Completed, CalendarEntryStatusEnum.AtRisk, CalendarEntryStatusEnum.Delayed, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.AtRisk] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.Delayed, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.Delayed] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.Completed, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.Completed] = new HashSet<CalendarEntryStatusEnum>(),
            [CalendarEntryStatusEnum.Cancelled] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.Scheduled }
        };
}

/// <summary>
/// Identifies the endpoint classification for Side B (Dependent) or Side A (Prerequisite) in WorkDependencyEntity.
/// </summary>
[ClassificationEnum]
public enum DependencyNodeTypeEnum
{
    [Display(Name = "Calendar / Milestone", Description = "A program or project calendar delivery gate (Milestone, Sprint, SIT, UAT, Vendor Delivery, Release, etc.)")]
    CalendarGate = 1,

    [Display(Name = "Task / Work Item", Description = "A program or project task or work item")]
    TaskItem = 2,

    [Display(Name = "Manual Dependency", Description = "A manually logged external, vendor, or ad-hoc RAID dependency")]
    ManualDependency = 3
}

/// <summary>
/// Unified calendar item DTO for rendering scheduled events and derived work item deadlines.
/// </summary>
public class CalendarItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string ItemCategory { get; set; } = "Event"; // "Event", "Task", "SubTask", "Risk", "Assumption", "Issue", "Blocker"
    public string PriorityOrCriticality { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public string OwnerOrAssignee { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#0078d4";
    public string BadgeIcon { get; set; } = "Calendar";
    public Guid? SourceTaskId { get; set; }
    public Guid? ScopeId { get; set; }
    public TaskLevelEnum Level { get; set; } = TaskLevelEnum.Project;
    public bool IsOverdue => Status != "Closed" && Status != "Completed" && EndUtc.Date < DateTime.UtcNow.Date;
}
