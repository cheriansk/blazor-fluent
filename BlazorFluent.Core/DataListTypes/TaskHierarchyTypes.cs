using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Organizational scoping level for a work item or calendar event.
/// </summary>
public enum TaskLevel
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
public enum TaskPriority
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
/// </summary>
public enum TaskStatus
{
    [Display(Name = "Open", Description = "Ready for work, awaiting triage")]
    Open = 1,

    [Display(Name = "In Progress", Description = "Work actively underway")]
    InProgress = 2,

    [Display(Name = "Closed", Description = "Deliverable completed and verified")]
    Closed = 3,

    [Display(Name = "Cancelled", Description = "Work item descoped or abandoned")]
    Cancelled = 4
}

/// <summary>
/// Discriminator classification for work items.
/// </summary>
public enum TaskItemType
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
/// Lifecycle status for an individual sequential step within a WorkflowTaskEntity.
/// </summary>
public enum WorkflowStepStatus
{
    [Display(Name = "Pending", Description = "Awaiting preceding step completion")]
    Pending = 1,

    [Display(Name = "Active", Description = "Currently actionable by designated step owner")]
    Active = 2,

    [Display(Name = "Completed", Description = "Successfully executed")]
    Completed = 3,

    [Display(Name = "Skipped", Description = "Bypassed by administrator")]
    Skipped = 4
}

/// <summary>
/// Impact severity rating for formal enterprise risks.
/// </summary>
public enum RiskImpact
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
public enum RiskProbability
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
public enum RiskStatus
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

/// <summary>
/// Validation status for an enterprise assumption (A of RAID).
/// </summary>
public enum AssumptionValidationStatus
{
    [Display(Name = "Unconfirmed", Description = "Assumption made, requires validation")]
    Unconfirmed = 1,

    [Display(Name = "Validated", Description = "Assumption verified as true")]
    Validated = 2,

    [Display(Name = "Invalidated", Description = "Assumption proven false, requires replanning")]
    Invalidated = 3
}

/// <summary>
/// Severity rating for an active issue (I of RAID).
/// </summary>
public enum IssueSeverity
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
public enum DependencyCriticality
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
public enum CalendarEntryType
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
    General = 8
}

/// <summary>
/// Status of a calendar entry.
/// </summary>
public enum CalendarEntryStatus
{
    Scheduled = 1,
    Confirmed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
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
    public TaskLevel Level { get; set; } = TaskLevel.Project;
    public bool IsOverdue => Status != "Closed" && Status != "Completed" && EndUtc.Date < DateTime.UtcNow.Date;
}
