using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Active impediment or operational issue ("I" of RAID).
/// Tracks realized problems actively hurting project or program delivery.
/// Can be scoped to Program or Project levels.
/// Implements <see cref="IMilestoneLinkedEntity"/> for optional milestone association.
/// </summary>
public class IssueEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    /// <summary>
    /// Optional foreign key to a project or program milestone.
    /// </summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Unmapped convenience alias mapping to and from <see cref="BaseTaskEntity.Priority"/> to avoid duplicate storage.
    /// </summary>
    [NotMapped]
    public IssueSeverityEnum Severity
    {
        get => Priority switch
        {
            TaskPriorityEnum.Low => IssueSeverityEnum.Low,
            TaskPriorityEnum.Medium => IssueSeverityEnum.Medium,
            TaskPriorityEnum.High => IssueSeverityEnum.High,
            TaskPriorityEnum.Urgent => IssueSeverityEnum.Critical,
            _ => IssueSeverityEnum.Medium
        };
        set => Priority = value switch
        {
            IssueSeverityEnum.Low => TaskPriorityEnum.Low,
            IssueSeverityEnum.Medium => TaskPriorityEnum.Medium,
            IssueSeverityEnum.High => TaskPriorityEnum.High,
            IssueSeverityEnum.Critical => TaskPriorityEnum.Urgent,
            _ => TaskPriorityEnum.Medium
        };
    }

    /// <summary>
    /// Investigation notes and identified root cause of this issue.
    /// </summary>
    public string? RootCause { get; set; }

    /// <summary>
    /// Temporary tactical workaround allowing partial progress while the permanent fix is developed.
    /// </summary>
    public string? Workaround { get; set; }

    /// <summary>
    /// Permanent resolution strategy and action plan.
    /// </summary>
    public string? ResolutionStrategy { get; set; }
}
