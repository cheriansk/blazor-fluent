namespace BlazorFluent.Shared.Contracts.Engine.Workflows;

public record WorkflowTransitionReqDto(
    Guid EntityId,
    string EntityType,
    string CurrentState,
    string ProposedState,
    string TriggeredByUserId,
    IReadOnlyDictionary<string, string> ContextData
);

public record WorkflowTransitionRespDto(
    Guid EntityId,
    bool IsAllowed,
    string NewState,
    IReadOnlyList<string> GuardViolations,
    IReadOnlyList<string> PostActionsToExecute
);
