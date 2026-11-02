namespace BlazorEnterprise.Core.Dtos;

public class RfpTimelineRowDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StepName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddDays(14);
    public string Details { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned";
}

public class ProjectPhaseDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PhaseName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? TargetDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    public string Status { get; set; } = "Planned";
}

public class ProjectMilestoneDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    public string Deliverable { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled";
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
    public string SelectedTemplateId { get; set; } = "base";

    // Step 1: Basic Project Details
    public Guid? ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string ScopeSummary { get; set; } = string.Empty;

    // Step 2: RFP Timeline
    public List<RfpTimelineRowDto> RfpTimelineRows { get; set; } = [];

    // Step 3: Project Timeline
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(6);
    public List<ProjectPhaseDto> Phases { get; set; } = [];

    // Step 4: Milestone Calendar
    public List<ProjectMilestoneDto> Milestones { get; set; } = [];

    // Step 5: Team Roster
    public List<ProjectRosterMemberDto> Roster { get; set; } = [];

    // Extensibility property bag for downstream custom steps
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
