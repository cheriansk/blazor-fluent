using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Abstractions;

public interface IBatchJobHandler<in TEvent> where TEvent : IJobEvent
{
    Task HandleAsync(TEvent jobEvent, CancellationToken cancellationToken);
}
