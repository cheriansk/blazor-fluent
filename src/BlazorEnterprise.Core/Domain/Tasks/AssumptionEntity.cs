using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Operational or strategic assumption ("A" of RAID).
/// Tracks factors believed to be true during planning, along with validation status and invalidation blast radius.
/// Can be scoped to Program or Project levels.
/// Implements <see cref="IMilestoneLinkedEntity"/> for optional milestone association.
/// </summary>
public class AssumptionEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    /// <summary>
    /// Optional foreign key to a project or program milestone.
    /// </summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Explicit criteria or tests required to validate this assumption.
    /// </summary>
    public required string ValidationCriteria { get; set; }

    /// <summary>
    /// Current validation status (Unconfirmed, Validated, Invalidated).
    /// </summary>
    public required AssumptionValidationStatusEnum ValidationStatus { get; set; }

    /// <summary>
    /// Downstream blast radius and impact on scope, schedule, or budget if this assumption proves false.
    /// </summary>
    public string? ImpactIfInvalid { get; set; }
}
