using BlazorEnterprise.Core.Events;

namespace BlazorEnterprise.Jobs.Abstractions;

public interface IBatchJobHandler<in TEvent> where TEvent : IJobEvent
{
    Task HandleAsync(TEvent jobEvent, CancellationToken cancellationToken);
}
