using BlazorEnterprise.Shared.Contracts.Engine.Workflows;

namespace BlazorEnterprise.RulesEngine.Workflows;

/// <summary>
/// Workflow transition state machine validator.
/// Evaluates lifecycle state changes, guard constraints, and post-transition triggers.
/// </summary>
public static class WorkflowTransitionEngine
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Draft"] = new(StringComparer.OrdinalIgnoreCase) { "PendingReview", "Cancelled" },
        ["PendingReview"] = new(StringComparer.OrdinalIgnoreCase) { "Approved", "Rejected", "Draft" },
        ["Approved"] = new(StringComparer.OrdinalIgnoreCase) { "InProgress", "Cancelled" },
        ["InProgress"] = new(StringComparer.OrdinalIgnoreCase) { "Completed", "Blocked", "Cancelled" },
        ["Blocked"] = new(StringComparer.OrdinalIgnoreCase) { "InProgress", "Cancelled" },
        ["Completed"] = new(StringComparer.OrdinalIgnoreCase) { "Archived", "Reopened" },
        ["Reopened"] = new(StringComparer.OrdinalIgnoreCase) { "InProgress" },
        ["Cancelled"] = new(StringComparer.OrdinalIgnoreCase) { "Draft" },
        ["Archived"] = new(StringComparer.OrdinalIgnoreCase)
    };

    public static WorkflowTransitionRespDto Evaluate(WorkflowTransitionReqDto request)
    {
        var violations = new List<string>();
        var postActions = new List<string>();

        // 1. Basic transition rule check
        if (!AllowedTransitions.TryGetValue(request.CurrentState, out var allowedNextStates) ||
            !allowedNextStates.Contains(request.ProposedState))
        {
            violations.Add($"Transition from state '{request.CurrentState}' to '{request.ProposedState}' is not permitted by workflow rules.");
            return new WorkflowTransitionRespDto(
                EntityId: request.EntityId,
                IsAllowed: false,
                NewState: request.CurrentState,
                GuardViolations: violations,
                PostActionsToExecute: []
            );
        }

        // 2. Guard conditions
        if (string.Equals(request.ProposedState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            if (request.ContextData.TryGetValue("HasOpenBlockers", out var blockers) &&
                bool.TryParse(blockers, out var hasBlockers) && hasBlockers)
            {
                violations.Add("Cannot transition to Completed while active blockers remain unresolved.");
            }
        }

        if (string.Equals(request.ProposedState, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.TriggeredByUserId))
            {
                violations.Add("Approval transition requires an authenticated approver identity.");
            }
        }

        var isAllowed = violations.Count == 0;
        if (isAllowed)
        {
            postActions.Add($"EmitTransitionAudit_{request.CurrentState}_To_{request.ProposedState}");
            if (string.Equals(request.ProposedState, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                postActions.Add("RecalculateParentMilestoneProgress");
            }
        }

        return new WorkflowTransitionRespDto(
            EntityId: request.EntityId,
            IsAllowed: isAllowed,
            NewState: isAllowed ? request.ProposedState : request.CurrentState,
            GuardViolations: violations,
            PostActionsToExecute: postActions
        );
    }
}
