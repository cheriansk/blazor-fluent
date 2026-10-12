using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Lifecycle status of an enterprise program.
/// </summary>
public enum ProgramStatus
{
    [Display(Name = "Planning", Description = "Program scope and roadmap being defined")]
    Planning = 1,

    [Display(Name = "Active", Description = "Program is actively running across member projects")]
    Active = 2,

    [Display(Name = "At Risk", Description = "Program faces critical milestones, blockers, or schedule variance")]
    AtRisk = 3,

    [Display(Name = "On Hold", Description = "Program is temporarily suspended")]
    OnHold = 4,

    [Display(Name = "Completed", Description = "All member projects and deliverables have concluded")]
    Completed = 5
}

/// <summary>
/// Tenant-level organizational role for a user within a client tenant.
/// </summary>
public enum TenantRole
{
    [Display(Name = "Tenant Admin", Description = "Full administrative control over tenant settings, programs, projects, and users")]
    TenantAdmin = 1,

    [Display(Name = "Tenant Member", Description = "General tenant member; requires explicit project or program assignment")]
    TenantMember = 2
}

/// <summary>
/// Program-level governance role for a user within a specific program.
/// </summary>
public enum ProgramRole
{
    [Display(Name = "Program Admin", Description = "Full control over the assigned program and all member projects under it")]
    ProgramAdmin = 1,

    [Display(Name = "Program Member", Description = "Read-only visibility into program delivery scorecard and member projects")]
    ProgramMember = 2
}

/// <summary>
/// Real-time health rating of a program rolled up from member projects.
/// </summary>
public enum ProgramHealthRating
{
    [Display(Name = "On Track", Description = "Member projects progressing on schedule without critical blockers")]
    OnTrack = 1,

    [Display(Name = "At Risk", Description = "One or more member projects experiencing schedule variance or cross-project blockers")]
    AtRisk = 2,

    [Display(Name = "Critical", Description = "Critical cross-project blockers overdue or major milestone deadlines missed")]
    Critical = 3
}

/// <summary>
/// Executive roll-up summary of a program's health, member projects, and cross-project blockers.
/// </summary>
public class ProgramHealthSummaryDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramCode { get; set; } = string.Empty;
    public ProgramStatus Status { get; set; }
    public ProgramHealthRating Health { get; set; } = ProgramHealthRating.OnTrack;
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int ProjectsAtRisk { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OpenTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int CrossProjectBlockersCount { get; set; }
    public double ProgressPercentage => TotalTasks == 0 ? 0 : Math.Round((double)CompletedTasks / TotalTasks * 100, 1);
    public List<CrossProjectDependencyDto> CrossProjectDependencies { get; set; } = new();
    public List<string> RiskReasons { get; set; } = new();
}

/// <summary>
/// Representation of a dependency crossing project boundaries within a program or tenant.
/// </summary>
public class CrossProjectDependencyDto
{
    public Guid DependencyId { get; set; }
    public Guid SourceProjectId { get; set; }
    public string SourceProjectName { get; set; } = string.Empty;
    public Guid SourceTaskId { get; set; }
    public string SourceTaskTitle { get; set; } = string.Empty;
    public Guid TargetProjectId { get; set; }
    public string TargetProjectName { get; set; } = string.Empty;
    public Guid TargetTaskId { get; set; }
    public string TargetTaskTitle { get; set; } = string.Empty;
    public string TargetAssigneeEmails { get; set; } = string.Empty;
    public TaskDependencyType DependencyType { get; set; }
    public DateTime? ResolveByUtc { get; set; }
    public bool IsResolved { get; set; }
    public bool IsOverdue => !IsResolved && ResolveByUtc.HasValue && ResolveByUtc.Value.Date < DateTime.UtcNow.Date;
}
