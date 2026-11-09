using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Dtos;

public class ProjectResourceDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserEntityId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public ProjectResourceRoleEnum Role { get; set; }
    public decimal? HourlyRate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
}

public class ResourceAllocationCellDto
{
    public Guid? Id { get; set; }
    public Guid ProjectResourceId { get; set; }
    public DateOnly Date { get; set; }
    public int AllocationPercentage { get; set; }
}

public class ResourceDailyAllocationDto
{
    public Guid ProjectResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public ProjectResourceRoleEnum Role { get; set; }
    public decimal HourlyRate { get; set; }
    public Dictionary<DateOnly, int> Allocations { get; set; } = new();
    public double TotalAllocatedHours { get; set; }
    public decimal TotalCost { get; set; }
}

public class ResourcePlanSummaryDto
{
    public Guid RevisionHeaderId { get; set; }
    public int VerNum { get; set; }
    public RevisionStatusEnum Status { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<DateOnly> Dates { get; set; } = new();
    public List<ResourceDailyAllocationDto> Resources { get; set; } = new();
    public Dictionary<DateOnly, int> DailyTotals { get; set; } = new();
    public decimal TotalPlanCost { get; set; }
    public bool IsLocked => Status != RevisionStatusEnum.Draft;
}
