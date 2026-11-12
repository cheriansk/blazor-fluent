using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Core.Dtos;

public class ProjectMilestoneDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public uint Version { get; set; }
    public Guid? ParentTimelineId { get; set; }
    public Guid? ParentPhaseId
    {
        get => ParentTimelineId;
        set => ParentTimelineId = value;
    }
    public string Title { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    public DateTime? DueDate
    {
        get => EndDate;
        set => EndDate = value;
    }
    public string OwnerEmails { get; set; } = string.Empty;
    public string Deliverable { get; set; } = string.Empty;
    [AllowHardcodedDefault("Initial default status for project milestone draft")]
    public string Status { get; set; } = "New";
    public string? Comments { get; set; }
}

public class ProjectPhaseDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public uint Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PhaseName
    {
        get => Title;
        set => Title = value;
    }
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    public DateTime? TargetDate
    {
        get => EndDate;
        set => EndDate = value;
    }
    [AllowHardcodedDefault("Initial default status for project phase draft")]
    public string Status { get; set; } = "New";
    public string? Comments { get; set; }
    public List<ProjectMilestoneDto> Milestones { get; set; } = [];
}

public class ProjectRosterMemberDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; }
}

public partial class CreateProjectWizardState
{
    [AllowHardcodedDefault("Wizard base template default discriminator")]
    public string SelectedTemplateId { get; set; } = "base";

    // Step 1: Basic Project Details
    public Guid? ProgramId { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string ScopeSummary { get; set; } = string.Empty;

    // Step 2: Project Timeline
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(6);
    public List<ProjectPhaseDto> Phases { get; set; } = [];

    // Step 3: Milestone Calendar
    public List<ProjectMilestoneDto> Milestones { get; set; } = [];

    // Step 4: Team Roster
    public List<ProjectRosterMemberDto> Roster { get; set; } = [];

    // Extensibility property bag for downstream custom steps
    [AllowHardcodedDefault("Extensibility property bag initialized as case-insensitive dictionary")]
    public Dictionary<string, object?> CustomStepData { get; } = new(StringComparer.OrdinalIgnoreCase);

    public T GetCustom<T>(string key, T fallback = default!)
    {
        if (CustomStepData.TryGetValue(key, out var val) && val is T typedVal)
        {
            return typedVal;
        }
        return fallback;
    }

    public void SetCustom<T>(string key, T value)
    {
        CustomStepData[key] = value;
    }
}
