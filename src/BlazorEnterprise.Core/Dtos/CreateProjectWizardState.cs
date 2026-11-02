namespace BlazorEnterprise.Core.Dtos;

public class RfpTimelineRowDto
{
    public string StepName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class ProjectPhaseDto
{
    public string PhaseName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? TargetDate { get; set; }
}

public class ProjectMilestoneDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string Deliverable { get; set; } = string.Empty;
}

public class ProjectRosterMemberDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
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
