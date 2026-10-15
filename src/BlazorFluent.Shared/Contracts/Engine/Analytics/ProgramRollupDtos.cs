using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Shared.Contracts.Engine.Analytics;

public record ProjectRollupInput(
    Guid ProjectId,
    int TotalTasks,
    int CompletedTasks,
    int OverdueTasks,
    int ActiveBlockers,
    decimal BudgetVariance,
    ProjectHealthRating Health
);

public record ProgramRollupReqDto(
    Guid ProgramId,
    IReadOnlyList<ProjectRollupInput> Projects
);

public record ProgramRollupRespDto(
    Guid ProgramId,
    ProgramHealthRating ComputedHealth,
    double AggregateProgressPercentage,
    int TotalActiveBlockers,
    decimal TotalBudgetVariance,
    IReadOnlyList<string> RiskAlerts
);
