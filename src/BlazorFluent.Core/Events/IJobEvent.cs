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
        string tenantId,
        string correlationId,
        string senderOrigin,
        string senderUserId,
        string senderUserEmail,
        Guid? parentExecutionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderOrigin);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderUserEmail);

        TriggerSource = triggerSource;
        TenantId = tenantId;
        CorrelationId = correlationId;
        SenderOrigin = senderOrigin;
        SenderUserId = senderUserId;
        SenderUserEmail = senderUserEmail;
        ParentExecutionId = parentExecutionId;
    }
}

