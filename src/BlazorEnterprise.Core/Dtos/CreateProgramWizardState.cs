using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Dtos;

public record ProgramAdminUserDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class ProgramMilestoneDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentTimelineId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Name
    {
        get => Title;
        set => Title = value;
    }
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);
    public DateTime TargetDate
    {
        get => EndDate ?? StartDate ?? DateTime.UtcNow.Date;
        set => EndDate = value;
    }
    public string OwnerEmails { get; set; } = string.Empty;
    public string Status { get; set; } = "New";
    public string? Comments { get; set; }
    public string? Description
    {
        get => Comments;
        set => Comments = value;
    }
}

public class ProgramTimelineRowDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(3);
    public string Status { get; set; } = "New";
    public string? Comments { get; set; }
    public List<ProgramMilestoneDto> Milestones { get; set; } = new();
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
