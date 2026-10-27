using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Extensions;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Formal enterprise risk entity ("R" of RAID).
/// Quantitative probability and impact matrix with exposure score, mitigation, and contingency plans.
/// Can be scoped to Tenant, Program, or Project levels.
/// Implements <see cref="IMilestoneLinkedEntity"/> for optional milestone association.
/// </summary>
public class RiskEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    /// <summary>
    /// Optional foreign key to a project or program milestone.
    /// </summary>
    public Guid? MilestoneId { get; set; }

    /// <summary>
    /// Financial, schedule, or operational impact rating (Low, Medium, High, Critical).
    /// </summary>
    public required RiskImpactEnum Impact { get; set; }

    /// <summary>
    /// Likelihood of risk realization (Low, Medium, High).
    /// </summary>
    public required RiskProbabilityEnum Probability { get; set; }

    /// <summary>
    /// Calculated risk exposure score (1 to 12).
    /// </summary>
    public int ExposureScore => (int)Impact * (int)Probability;

    /// <summary>
    /// Planned proactive mitigation actions to reduce probability or impact.
    /// </summary>
    public string? MitigationStrategy { get; set; }

    /// <summary>
    /// Contingency actions to execute if the risk materializes into an active Issue.
    /// </summary>
    public string? ContingencyPlan { get; set; }

    /// <summary>
    /// Governance lifecycle status of the risk.
    /// </summary>
    public required RiskStatusEnum RiskStatus { get; set; }

    /// <summary>Risk category discriminator for BaseTaskEntity transition validation.</summary>
    public override string GetDefaultCategory() => TaskStatusEnumDefinitions.Categories.Risk;

    /// <summary>
    /// Executes a validated state transition for this risk's governance status.
    /// </summary>
    public virtual void TransitionRiskStatusTo(RiskStatusEnum nextStatus, DateTime utcNow, string reason = "")
    {
        if (RiskStatus == nextStatus) return;
        if (!RiskStatus.CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException($"Cannot transition risk '{Title}' from {RiskStatus} to {nextStatus}.");
        }
        RiskStatus = nextStatus;
    }
}
