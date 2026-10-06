using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Jobs.Tasks;

/// <summary>
/// Daily batch job event triggering the end-of-day task status digest to Microsoft Teams channels.
/// </summary>
public record DailyTaskSummaryJobEvent(
    string TriggerSource = "Cron",
    string? SpecificTenantId = null,
    string? SenderOrigin = null,
    string? SenderUserId = null,
    string? SenderUserEmail = null
) : BaseJobEvent("DailyTaskSummaryJob", SpecificTenantId ?? "system", senderOrigin: SenderOrigin, senderUserId: SenderUserId, senderUserEmail: SenderUserEmail);
