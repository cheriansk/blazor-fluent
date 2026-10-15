using BlazorFluent.Shared.Contracts.Engine.Analytics;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.RulesEngine.Analytics;

/// <summary>
/// Proprietary calculation engine for multi-project strategic rollups.
/// Evaluates health, schedule velocity, budget variances, and blocker aggregation.
/// </summary>
public static class ProgramRollupEngine
{
    public static ProgramRollupRespDto ComputeRollup(ProgramRollupReqDto request)
    {
        if (request.Projects == null || request.Projects.Count == 0)
        {
            return new ProgramRollupRespDto(
                ProgramId: request.ProgramId,
                ComputedHealth: ProgramHealthRating.OnTrack,
                AggregateProgressPercentage: 0,
                TotalActiveBlockers: 0,
                TotalBudgetVariance: 0m,
                RiskAlerts: []
            );
        }

        var totalTasks = request.Projects.Sum(p => p.TotalTasks);
        var completedTasks = request.Projects.Sum(p => p.CompletedTasks);
        var totalBlockers = request.Projects.Sum(p => p.ActiveBlockers);
        var totalBudgetVariance = request.Projects.Sum(p => p.BudgetVariance);
        var overdueTasks = request.Projects.Sum(p => p.OverdueTasks);

        var progressPct = totalTasks == 0 ? 0 : Math.Round((double)completedTasks / totalTasks * 100, 1);

        var riskAlerts = new List<string>();

        // Heuristic Health Evaluation
        var criticalProjects = request.Projects.Count(p => p.Health == ProjectHealthRating.Critical);
        var atRiskProjects = request.Projects.Count(p => p.Health == ProjectHealthRating.AtRisk);

        ProgramHealthRating computedHealth;

        if (criticalProjects > 0 || totalBlockers >= 5 || overdueTasks >= 10)
        {
            computedHealth = ProgramHealthRating.Critical;
            if (criticalProjects > 0) riskAlerts.Add($"{criticalProjects} project(s) in Critical status.");
            if (totalBlockers >= 5) riskAlerts.Add($"High blocker volume: {totalBlockers} active blockers across program.");
            if (overdueTasks >= 10) riskAlerts.Add($"Schedule slippage: {overdueTasks} overdue tasks across program.");
        }
        else if (atRiskProjects > 0 || totalBlockers >= 2 || overdueTasks >= 3 || totalBudgetVariance < -50000m)
        {
            computedHealth = ProgramHealthRating.AtRisk;
            if (atRiskProjects > 0) riskAlerts.Add($"{atRiskProjects} project(s) currently marked AtRisk.");
            if (totalBudgetVariance < -50000m) riskAlerts.Add($"Negative budget variance: {totalBudgetVariance:C}");
        }
        else
        {
            computedHealth = ProgramHealthRating.OnTrack;
        }

        return new ProgramRollupRespDto(
            ProgramId: request.ProgramId,
            ComputedHealth: computedHealth,
            AggregateProgressPercentage: progressPct,
            TotalActiveBlockers: totalBlockers,
            TotalBudgetVariance: totalBudgetVariance,
            RiskAlerts: riskAlerts
        );
    }
}
