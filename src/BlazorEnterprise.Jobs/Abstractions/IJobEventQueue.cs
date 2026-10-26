using BlazorEnterprise.Core.Events;

namespace BlazorEnterprise.Jobs.Abstractions;

public interface IJobEventQueue
{
    ValueTask EnqueueAsync(IJobEvent jobEvent, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IJobEvent> ReadAllAsync(CancellationToken cancellationToken = default);
    void Complete();
}
