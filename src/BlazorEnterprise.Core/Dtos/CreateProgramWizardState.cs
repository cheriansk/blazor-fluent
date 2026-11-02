using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Dtos;

public record ProgramAdminUserDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public record ProgramTimelineRowDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(3);
    public string Status { get; set; } = "Planned";
}

public record ProgramMilestoneDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime TargetDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    public string? Description { get; set; }
    public string Status { get; set; } = "Scheduled";
}

public partial class CreateProgramWizardState
{
    public Guid? ProgramId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProgramStatusEnum Status { get; set; } = ProgramStatusEnum.Planning;
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddYears(1);

    public List<ProgramAdminUserDto> AdminUsers { get; set; } = new();
    public List<ProgramTimelineRowDto> TimelineRows { get; set; } = new();
    public List<ProgramMilestoneDto> Milestones { get; set; } = new();

    /// <summary>
    /// Extension point for downstream clones to attach custom step data without modifying base schema.
    /// </summary>
    public Dictionary<string, object> CustomStepData { get; set; } = new();
}
