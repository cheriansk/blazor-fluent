using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Web.Models.Dashboard;

/// <summary>
/// View model for displaying project health, status, role, and progress in the Home Page card layout.
/// Scoped strictly to projects where the active user is an authorized member.
/// </summary>
public sealed class ProjectCardItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public ProjectStatusEnum Status { get; set; }
    public ProjectHealthRatingEnum Health { get; set; } = ProjectHealthRatingEnum.OnTrack;
    public ProjectRoleEnum? UserRole { get; set; }
    public double ProgressPercentage { get; set; }
    public int CompletedTasks { get; set; }
    public int TotalTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int OpenTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int ActiveBlockersCount { get; set; }
    public DateTime? DueDate { get; set; }
}
