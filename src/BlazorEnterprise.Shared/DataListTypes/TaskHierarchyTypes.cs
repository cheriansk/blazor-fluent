using BlazorEnterprise.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorEnterprise.Shared.DataListTypes;

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

    [Display(Name = "User", Description = "Personal user workspace")]
    User = 4
}

/// <summary>
/// Identifies entity type in polymorphic target-centric edge relations.
/// </summary>
[ClassificationEnum]
public enum RelationEntityTypeEnum
{
    [Display(Name = "Task", Description = "Task deliverable")]
    Task = 1,

    [Display(Name = "Risk", Description = "Enterprise risk")]
    Risk = 2,

    [Display(Name = "Issue", Description = "Active issue or impediment")]
    Issue = 3,

    [Display(Name = "Assumption", Description = "Operational assumption")]
    Assumption = 4,

    [Display(Name = "Workflow", Description = "Governing workflow pipeline")]
    Workflow = 5,

    [Display(Name = "WorkDependency", Description = "Cross-work dependency")]
    WorkDependency = 6,

    [Display(Name = "Calendar", Description = "Unified timeline/milestone calendar")]
    Calendar = 7,

    [Display(Name = "RevisionHeader", Description = "Scenario baseline revision header")]
    RevisionHeader = 8,

    [Display(Name = "KnowledgeArticle", Description = "Knowledge base article")]
    KnowledgeArticle = 9,

    [Display(Name = "Notification", Description = "System or project alert notification")]
    Notification = 10,

    [Display(Name = "Deliverable", Description = "Project or meeting deliverable")]
    Deliverable = 11
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
    Workflow = 7,
    Deliverable = 8
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
    [Display(Name = "Timeline", Description = "High-level timeline phase or delivery track")]
    Timeline = 1,

    [Display(Name = "Milestone", Description = "Strategic deliverable checkpoint or stage gate")]
    Milestone = 2,

    [Display(Name = "Personal", Description = "Personal user calendar appointment or reminder")]
    Personal = 3,

    [Display(Name = "Out of Office / PTO", Description = "Planned time off, vacation, or out of office availability")]
    OutOfOffice = 4
}

/// <summary>
/// Status of a calendar entry.
/// </summary>
[LifecycleEnum]
public enum CalendarEntryStatusEnum
{
    New = 1,
    Scheduled = 2,
    Confirmed = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
    AtRisk = 7,
    Delayed = 8
}

public static class CalendarEntryStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<CalendarEntryStatusEnum, IReadOnlySet<CalendarEntryStatusEnum>> AllowedTransitions =
        new Dictionary<CalendarEntryStatusEnum, IReadOnlySet<CalendarEntryStatusEnum>>
        {
            [CalendarEntryStatusEnum.New] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.Scheduled, CalendarEntryStatusEnum.Confirmed, CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.Scheduled] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.Confirmed, CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.Confirmed] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.AtRisk, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.InProgress] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.Completed, CalendarEntryStatusEnum.AtRisk, CalendarEntryStatusEnum.Delayed, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.AtRisk] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.Delayed, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.Delayed] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.InProgress, CalendarEntryStatusEnum.Completed, CalendarEntryStatusEnum.Cancelled },
            [CalendarEntryStatusEnum.Completed] = new HashSet<CalendarEntryStatusEnum>(),
            [CalendarEntryStatusEnum.Cancelled] = new HashSet<CalendarEntryStatusEnum> { CalendarEntryStatusEnum.New, CalendarEntryStatusEnum.Scheduled }
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
    ManualDependency = 3,

    [Display(Name = "Sprint Task", Description = "A sprint-committed task deliverable")]
    SprintTask = 4,

    [Display(Name = "Workflow Task", Description = "A workflow pipeline step task")]
    WorkflowTask = 5,

    [Display(Name = "Misc Task", Description = "An ad-hoc or standalone backlog task")]
    MiscTask = 6,

    [Display(Name = "Risk", Description = "An enterprise or project risk")]
    RaidRisk = 7,

    [Display(Name = "Issue", Description = "An active project or program issue")]
    RaidIssue = 8,

    [Display(Name = "Assumption", Description = "A project or program assumption")]
    RaidAssumption = 9,

    [Display(Name = "Deliverable", Description = "A project or meeting deliverable outcome")]
    Deliverable = 10
}

/// <summary>
/// Lifecycle approval and verification status for deliverables.
/// </summary>
[LifecycleEnum]
public enum DeliverableStatusEnum
{
    [Display(Name = "Draft", Description = "Deliverable is currently in authoring phase")]
    Draft = 1,

    [Display(Name = "Under Review", Description = "Awaiting formal stakeholder sign-off")]
    UnderReview = 2,

    [Display(Name = "Approved", Description = "Officially accepted by the lead or authority")]
    Approved = 3,

    [Display(Name = "Rejected", Description = "Returned to contributor for revisions")]
    Rejected = 4
}

public static class DeliverableStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<DeliverableStatusEnum, IReadOnlySet<DeliverableStatusEnum>> AllowedTransitions =
        new Dictionary<DeliverableStatusEnum, IReadOnlySet<DeliverableStatusEnum>>
        {
            [DeliverableStatusEnum.Draft] = new HashSet<DeliverableStatusEnum> { DeliverableStatusEnum.UnderReview, DeliverableStatusEnum.Rejected },
            [DeliverableStatusEnum.UnderReview] = new HashSet<DeliverableStatusEnum> { DeliverableStatusEnum.Approved, DeliverableStatusEnum.Rejected, DeliverableStatusEnum.Draft },
            [DeliverableStatusEnum.Approved] = new HashSet<DeliverableStatusEnum> { DeliverableStatusEnum.UnderReview },
            [DeliverableStatusEnum.Rejected] = new HashSet<DeliverableStatusEnum> { DeliverableStatusEnum.Draft }
        };
}

/// <summary>
/// Supported parent work item types that can have deliverables attached.
/// SubTasks and WorkDependencies are strictly excluded.
/// </summary>
[ClassificationEnum]
public enum DeliverableParentTypeEnum
{
    [Display(Name = "Task", Description = "Backlog, sprint, or workflow task")]
    Task = 1,

    [Display(Name = "Risk", Description = "Enterprise or project risk requiring deliverables")]
    Risk = 2,

    [Display(Name = "Issue", Description = "Active project issue requiring resolution deliverables")]
    Issue = 3,

    [Display(Name = "Assumption", Description = "Operational assumption requiring validation deliverables")]
    Assumption = 4,

    [Display(Name = "Calendar / Meeting", Description = "Milestone checkpoint or meeting outcome")]
    Calendar = 5,

    [Display(Name = "Workflow", Description = "Governing workflow pipeline")]
    Workflow = 6
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
    public string? OwnerEmails { get; set; }
    public string? Comments { get; set; }
    public Guid? ParentTimelineId { get; set; }
    public DateTime? DueDate { get; set; }
    public string ColorHex { get; set; } = "#0078d4";
    public string BadgeIcon { get; set; } = "Calendar";
    public Guid? SourceTaskId { get; set; }
    public Guid? ScopeId { get; set; }
    public TaskLevelEnum Level { get; set; } = TaskLevelEnum.Project;
    public bool IsOverdue => Status != "Closed" && Status != "Completed" && EndUtc.Date < DateTime.UtcNow.Date;
}
