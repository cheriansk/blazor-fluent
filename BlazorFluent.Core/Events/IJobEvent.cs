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
}

public abstract record BaseJobEvent(string TriggerSource, string? TenantId = null) : IJobEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string TriggerSource { get; init; } = TriggerSource;
    public string? TenantId { get; init; } = TenantId;
}
