using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Jobs.Tasks;

/// <summary>
/// Daily batch job event triggering the end-of-day task status digest to Microsoft Teams channels.
/// </summary>
public record DailyTaskSummaryJobEvent(
    string TriggerSource,
    string TenantId,
    string CorrelationId,
    string SenderOrigin,
    string SenderUserId,
    string SenderUserEmail,
    Guid? ParentExecutionId = null
) : BaseJobEvent(TriggerSource, TenantId, CorrelationId, SenderOrigin, SenderUserId, SenderUserEmail, ParentExecutionId);
