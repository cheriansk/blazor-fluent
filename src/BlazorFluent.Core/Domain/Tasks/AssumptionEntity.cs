using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Operational or strategic assumption ("A" of RAID).
/// Tracks factors believed to be true during planning, along with validation status and invalidation blast radius.
/// Can be scoped to Program or Project levels.
/// </summary>
public class AssumptionEntity : BaseTaskEntity
{
    /// <summary>
    /// Explicit criteria or tests required to validate this assumption.
    /// </summary>
    public string ValidationCriteria { get; set; } = string.Empty;

    /// <summary>
    /// Current validation status (Unconfirmed, Validated, Invalidated).
    /// </summary>
    public AssumptionValidationStatus ValidationStatus { get; set; } = AssumptionValidationStatus.Unconfirmed;

    /// <summary>
    /// Downstream blast radius and impact on scope, schedule, or budget if this assumption proves false.
    /// </summary>
    public string ImpactIfInvalid { get; set; } = string.Empty;
}
