using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

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
    public IssueSeverity Severity
    {
        get => Priority switch
        {
            TaskPriority.Low => IssueSeverity.Low,
            TaskPriority.Medium => IssueSeverity.Medium,
            TaskPriority.High => IssueSeverity.High,
            TaskPriority.Urgent => IssueSeverity.Critical,
            _ => IssueSeverity.Medium
        };
        set => Priority = value switch
        {
            IssueSeverity.Low => TaskPriority.Low,
            IssueSeverity.Medium => TaskPriority.Medium,
            IssueSeverity.High => TaskPriority.High,
            IssueSeverity.Critical => TaskPriority.Urgent,
            _ => TaskPriority.Medium
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
