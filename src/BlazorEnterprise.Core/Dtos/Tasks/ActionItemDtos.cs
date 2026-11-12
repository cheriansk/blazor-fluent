using BlazorEnterprise.Core.Domain.Tasks;

namespace BlazorEnterprise.Core.Dtos.Tasks;

/// <summary>
/// DTO representing an action item checklist entry.
/// </summary>
public class ActionItemDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid ParentTaskId { get; set; }
    public Guid? ParentSubTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ActionItemStatusEnum Status { get; set; }
    public int Complexity { get; set; }
    public decimal EstimatedHours { get; set; }
    public int OrderIndex { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockingReason { get; set; }
    public DateTime Created { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating an action item.
/// </summary>
public class CreateActionItemDto
{
    public Guid? ParentSubTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ActionItemStatusEnum Status { get; set; }
    public int Complexity { get; set; }
    public decimal EstimatedHours { get; set; }
    public int OrderIndex { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockingReason { get; set; }
}

/// <summary>
/// DTO for updating an action item.
/// </summary>
public class UpdateActionItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ActionItemStatusEnum Status { get; set; }
    public int Complexity { get; set; }
    public decimal EstimatedHours { get; set; }
    public int OrderIndex { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockingReason { get; set; }
    public Guid? ParentSubTaskId { get; set; }
}
