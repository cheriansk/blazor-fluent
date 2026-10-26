using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Shared.Contracts.Engine.Analytics;

public record ProjectRollupInput(
    Guid ProjectId,
    int TotalTasks,
    int CompletedTasks,
    int OverdueTasks,
    int ActiveBlockers,
    decimal BudgetVariance,
    ProjectHealthRatingEnum Health
);

public record ProgramRollupReqDto(
    Guid ProgramId,
    IReadOnlyList<ProjectRollupInput> Projects
);

public record ProgramRollupRespDto(
    Guid ProgramId,
    ProgramHealthRatingEnum ComputedHealth,
    double AggregateProgressPercentage,
    int TotalActiveBlockers,
    decimal TotalBudgetVariance,
    IReadOnlyList<string> RiskAlerts
);
