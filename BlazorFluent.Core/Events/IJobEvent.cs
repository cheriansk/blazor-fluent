namespace BlazorFluent.Core.Events;

public interface IJobEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
    string TriggerSource { get; }
}

public abstract record BaseJobEvent(string TriggerSource) : IJobEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string TriggerSource { get; init; } = TriggerSource;
}
