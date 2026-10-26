using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Events;

/// <summary>
/// Persistent ledger record capturing an event publication:
/// who emitted it (source class and method), acting user, payload JSON, and aggregate status.
/// Implements <see cref="ITenantEntity"/> for tenancy scoping,
/// <see cref="IAuditExemptEntity"/> to prevent recursive auditing,
/// </summary>
public class EventPublishTrackerEntity : AuditableEntity, ITenantEntity, IAuditExemptEntity, IImmutableEntity
{
    /// <summary>Tenant scope for row-level isolation. Multi-tenant queries filter by this value.</summary>
    public required Guid TenantId { get; set; }

    /// <summary>Unique identifier of the published event (UUIDv7).</summary>
    public Guid EventId { get; set; } = Guid.CreateVersion7();

    /// <summary>Correlation identifier linking all downstream consumer steps and workflow chains.</summary>
    public required string CorrelationId { get; set; }

    /// <summary>Short name of the event type (e.g. 'TaskCadenceAlertJobEvent', 'ArticleApprovedEvent').</summary>
    public required string EventName { get; set; }

    /// <summary>Full assembly-qualified or CLR type name of the event.</summary>
    public string? EventTypeFullName { get; set; }

    /// <summary>Class name that published or dispatched the event.</summary>
    public required string SourceClass { get; set; }

    /// <summary>Method name that published or dispatched the event.</summary>
    public required string SourceMethod { get; set; }

    /// <summary>Source file path where the dispatch call originated (optional).</summary>
    public string? SourceFilePath { get; set; }

    /// <summary>ID of the user who initiated the event trigger, or 'system' for background daemons.</summary>
    public string? UserId { get; set; }

    /// <summary>Email or username of the acting user.</summary>
    public string? UserEmail { get; set; }

    /// <summary>Trigger source classification: 'Manual', 'Cron', 'API', 'System', 'Event'.</summary>
    public required string TriggerSource { get; set; }

    /// <summary>Full JSON payload of the event at publication time.</summary>
    public required string PayloadJson { get; set; }

    /// <summary>Timestamp (UTC) when the event was emitted.</summary>
    public required DateTime PublishedAtUtc { get; set; }

    /// <summary>Overall lifecycle status of the published event.</summary>
    public required EventTrackerStatusEnum Status { get; set; }

    /// <summary>Count of consumers that have picked up or executed this event.</summary>
    public int ConsumerCount { get; set; }

    /// <summary>Consumer executions recorded for this event.</summary>
    public ICollection<EventConsumptionTrackerEntity> Consumptions { get; set; } = new List<EventConsumptionTrackerEntity>();

    /// <summary>
    /// Computes who or what sent the event:
    /// - If sent by user interaction: strictly points to that user (Email or UserId).
    /// - If sent by batch or background daemon: uses the daemon user running the batch.
    /// </summary>
    public string GetEffectiveSender()
    {
        if (!string.IsNullOrWhiteSpace(UserEmail) &&
            !BlazorEnterprise.Core.Utilities.SystemIdentityUtility.IsSystemDaemon(UserEmail))
        {
            return UserEmail;
        }

        if (!string.IsNullOrWhiteSpace(UserId) &&
            !BlazorEnterprise.Core.Utilities.SystemIdentityUtility.IsSystemDaemon(UserId) &&
            !UserId.StartsWith("ChannelJob:", StringComparison.OrdinalIgnoreCase) &&
            !UserId.StartsWith("SystemDaemon", StringComparison.OrdinalIgnoreCase))
        {
            return UserId;
        }

        var batchName = !string.IsNullOrWhiteSpace(EventName)
            ? EventName.Replace("Event", string.Empty)
            : (!string.IsNullOrWhiteSpace(SourceClass) && SourceClass != "ChannelJobEventQueue" ? SourceClass : "BatchWorker");

        return $"SystemDaemon ({batchName})";
    }

    /// <summary>
    /// Computes the sender origin:
    /// - If batch: the batch name (e.g. TaskCadenceAlertJob)
    /// - If button on razor page: the button name (e.g. Button:RunTaskCadenceAlertJob)
    /// - If class function: the functionName (e.g. ImportFileService.UploadBatchAsync)
    /// </summary>
    public string GetEffectiveSenderOrigin()
    {
        if (!string.IsNullOrWhiteSpace(TriggerSource) &&
            (TriggerSource.StartsWith("Button:", StringComparison.OrdinalIgnoreCase) ||
             TriggerSource.StartsWith("Batch:", StringComparison.OrdinalIgnoreCase) ||
             TriggerSource.Contains(".")))
        {
            return TriggerSource;
        }

        if (string.Equals(TriggerSource, "Cron", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(TriggerSource, "System", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(SourceClass, "ChannelJobEventQueue", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(SourceClass, "PeriodicBatchScheduler", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(EventName) ? EventName.Replace("Event", string.Empty) : "BatchJob";
        }

        if (SourceClass.EndsWith("Page", StringComparison.OrdinalIgnoreCase) ||
            SourceClass.EndsWith("Comp", StringComparison.OrdinalIgnoreCase) ||
            SourceClass.EndsWith("Tab", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(SourceMethod) ? $"Button:{SourceMethod.Replace("Async", string.Empty)}" : SourceClass;
        }

        if (!string.IsNullOrWhiteSpace(SourceMethod))
        {
            return !string.IsNullOrWhiteSpace(SourceClass) && SourceClass != "ChannelJobEventQueue"
                ? $"{SourceClass}.{SourceMethod}"
                : SourceMethod;
        }

        return !string.IsNullOrWhiteSpace(EventName) ? EventName.Replace("Event", string.Empty) : "UnknownOrigin";
    }
}
