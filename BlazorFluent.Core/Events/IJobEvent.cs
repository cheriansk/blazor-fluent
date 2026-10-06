namespace BlazorFluent.Core.Events;

public interface IJobEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
    string TriggerSource { get; }

    /// <summary>
    /// Job name inferred from the event type (e.g. AuditPurgeJobEvent -> AuditPurgeJob).
    /// </summary>
    string JobName => GetType().Name.Replace("Event", string.Empty);

    /// <summary>
    /// The tenant that enqueued this job. Restored by the background listener
    /// to scope all EF queries and auditing within the job to the correct tenant.
    /// Never null; defaults to 'system' or 'host' for cross-tenant maintenance jobs.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// Correlation identifier linking all chained batch steps belonging to the same workflow.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Identifier of the immediate predecessor job execution that triggered this step (if chained).
    /// </summary>
    Guid? ParentExecutionId { get; }

    /// <summary>
    /// Who sent the event: batch name, button name, or function name. Never null.
    /// </summary>
    string SenderOrigin { get; }

    /// <summary>
    /// User ID of the initiator if sent by user interaction, or daemon identity if batch. Never null.
    /// </summary>
    string SenderUserId { get; }

    /// <summary>
    /// Email of human user, or identical daemon identity if batch. Never null.
    /// </summary>
    string SenderUserEmail { get; }
}

public abstract record BaseJobEvent : IJobEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string TriggerSource { get; init; }
    public virtual string JobName => GetType().Name.Replace("Event", string.Empty);
    public string TenantId { get; init; }
    public string CorrelationId { get; init; }
    public Guid? ParentExecutionId { get; init; }
    public string SenderOrigin { get; init; }
    public string SenderUserId { get; init; }
    public string SenderUserEmail { get; init; }

    protected BaseJobEvent(
        string triggerSource,
        string? tenantId = null,
        string? correlationId = null,
        Guid? parentExecutionId = null,
        string? senderOrigin = null,
        string? senderUserId = null,
        string? senderUserEmail = null)
    {
        var jobName = GetType().Name.Replace("Event", string.Empty);

        TriggerSource = !string.IsNullOrWhiteSpace(triggerSource)
            ? triggerSource
            : (!string.IsNullOrWhiteSpace(senderOrigin) ? senderOrigin : "System");

        TenantId = !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId
            : "system";

        CorrelationId = !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId
            : Guid.CreateVersion7().ToString("N")[..12];

        ParentExecutionId = parentExecutionId;

        SenderOrigin = !string.IsNullOrWhiteSpace(senderOrigin)
            ? senderOrigin
            : (!string.IsNullOrWhiteSpace(triggerSource) ? triggerSource : jobName);

        var defaultDaemonId = $"SystemDaemon ({jobName})";

        SenderUserId = !string.IsNullOrWhiteSpace(senderUserId)
            ? senderUserId
            : defaultDaemonId;

        // For human user, if email is provided use it; for batch user (or missing email), userEmail matches userId
        SenderUserEmail = !string.IsNullOrWhiteSpace(senderUserEmail)
            ? senderUserEmail
            : (!string.IsNullOrWhiteSpace(senderUserId) ? senderUserId : defaultDaemonId);
    }
}

