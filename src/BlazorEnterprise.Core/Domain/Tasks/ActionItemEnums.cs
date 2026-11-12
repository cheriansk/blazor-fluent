using System.ComponentModel.DataAnnotations;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Execution and lifecycle status for a task action item (checklist item).
/// </summary>
public enum ActionItemStatusEnum
{
    [Display(Name = "Open", Description = "Checklist item is pending action")]
    Open = 1,

    [Display(Name = "In Progress", Description = "Checklist item is currently being worked on")]
    InProgress = 2,

    [Display(Name = "Completed", Description = "Checklist item has been completed")]
    Completed = 3,

    [Display(Name = "Blocked", Description = "Checklist item is blocked by an external factor")]
    Blocked = 4,

    [Display(Name = "Deferred", Description = "Checklist item has been postponed")]
    Deferred = 5
}

/// <summary>
/// Complexity estimate for an action item.
/// </summary>
public enum ActionItemComplexityEnum
{
    [Display(Name = "Low (1 pt)", Description = "Straightforward checklist item")]
    Low = 1,

    [Display(Name = "Medium (3 pts)", Description = "Moderate complexity checklist item")]
    Medium = 3,

    [Display(Name = "High (5 pts)", Description = "High complexity checklist item")]
    High = 5
}
