using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Abstractions;

public interface IJobEventQueue
{
    ValueTask EnqueueAsync(IJobEvent jobEvent, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IJobEvent> ReadAllAsync(CancellationToken cancellationToken = default);
}
