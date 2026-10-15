namespace BlazorFluent.Shared.Contracts.Engine.Analytics;

public record DependencyEdge(
    Guid SourceId,
    Guid TargetId,
    string DependencyType
);

public record DependencyDagReqDto(
    Guid ProjectId,
    IReadOnlyList<Guid> NodeIds,
    IReadOnlyList<DependencyEdge> Edges
);

public record DependencyDagRespDto(
    Guid ProjectId,
    bool HasCycle,
    IReadOnlyList<IReadOnlyList<Guid>> Cycles,
    IReadOnlyList<Guid> CriticalPath
);
