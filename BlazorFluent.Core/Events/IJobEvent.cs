namespace BlazorFluent.Core.Events;

public interface IJobEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
    string TriggerSource { get; }

    /// <summary>
    /// The tenant that enqueued this job. Restored by the background listener
    /// to scope all EF queries and auditing within the job to the correct tenant.
    /// Null only for host-level jobs that intentionally operate cross-tenant.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Correlation identifier linking all chained batch steps belonging to the same workflow.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Identifier of the immediate predecessor job execution that triggered this step (if chained).
    /// </summary>
    Guid? ParentExecutionId { get; }
}

public abstract record BaseJobEvent(
    string TriggerSource,
    string? TenantId = null,
    string? CorrelationId = null,
    Guid? ParentExecutionId = null) : IJobEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string TriggerSource { get; init; } = TriggerSource;
    public string? TenantId { get; init; } = TenantId;
    public string CorrelationId { get; init; } = !string.IsNullOrWhiteSpace(CorrelationId)
        ? CorrelationId
        : Guid.CreateVersion7().ToString("N")[..12];
    public Guid? ParentExecutionId { get; init; } = ParentExecutionId;
}

