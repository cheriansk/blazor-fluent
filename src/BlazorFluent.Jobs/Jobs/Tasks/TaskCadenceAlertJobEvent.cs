using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Jobs.Tasks;

/// <summary>
/// Scheduled batch event driving the enterprise delivery cadence alert engine.
/// Evaluates approaching deadlines (T-5, T-3, T-1, T-0), overdue tasks, and blocking dependencies.
/// </summary>
public record TaskCadenceAlertJobEvent(
    string TriggerSource,
    string TenantId,
    string CorrelationId,
    string SenderOrigin,
    string SenderUserId,
    string SenderUserEmail,
    Guid? ParentExecutionId = null
) : BaseJobEvent(TriggerSource, TenantId, CorrelationId, SenderOrigin, SenderUserId, SenderUserEmail, ParentExecutionId);
