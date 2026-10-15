using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Formal enterprise risk entity ("R" of RAID).
/// Quantitative probability and impact matrix with exposure score, mitigation, and contingency plans.
/// Can be scoped to Tenant, Program, or Project levels.
/// </summary>
public class RiskEntity : BaseTaskEntity
{
    /// <summary>
    /// Financial, schedule, or operational impact rating (Low, Medium, High, Critical).
    /// </summary>
    public RiskImpact Impact { get; set; } = RiskImpact.Medium;

    /// <summary>
    /// Likelihood of risk realization (Low, Medium, High).
    /// </summary>
    public RiskProbability Probability { get; set; } = RiskProbability.Medium;

    /// <summary>
    /// Calculated risk exposure score (1 to 12).
    /// </summary>
    public int ExposureScore => (int)Impact * (int)Probability;

    /// <summary>
    /// Planned proactive mitigation actions to reduce probability or impact.
    /// </summary>
    public string MitigationStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Contingency actions to execute if the risk materializes into an active Issue.
    /// </summary>
    public string ContingencyPlan { get; set; } = string.Empty;

    /// <summary>
    /// Governance lifecycle status of the risk.
    /// </summary>
    public RiskStatus RiskStatus { get; set; } = RiskStatus.Identified;
}
