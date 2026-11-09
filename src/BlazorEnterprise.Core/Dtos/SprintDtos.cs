using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Dtos;

public class SprintDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int SprintNumber { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Comments { get; set; }
    public SprintStatusEnum Status { get; set; }
    public int ItemCount { get; set; }
    public double TotalPlannedHours { get; set; }
    public DateTime Created { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime Updated { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class SprintMetricsDto
{
    public Guid SprintId { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public int SprintNumber { get; set; }
    public SprintStatusEnum Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double PlannedHours { get; set; }
    public double CompletedHours { get; set; }
    public double BlockedHours { get; set; }
    public double RemainingHours { get; set; }
    public double CompletionPercentage { get; set; }
    public double TeamVelocity { get; set; }
    public int TotalItemCount { get; set; }
}

public class SprintItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? SprintId { get; set; }
    public string? SprintName { get; set; }
    public TaskStatusEnum Status { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ResourceCapacityDto
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public ProjectResourceRoleEnum Role { get; set; }
    public double AvailableCapacityHours { get; set; }
    public double AllocatedHours { get; set; }
    public double RemainingCapacityHours => Math.Max(0, AvailableCapacityHours - AllocatedHours);
}
