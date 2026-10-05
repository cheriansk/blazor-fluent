using BlazorFluent.Core.Events;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Contract for publishing batch job events from domain or persistence services
/// without coupling to the background worker implementation.
/// </summary>
public interface IJobEventPublisher
{
    ValueTask PublishAsync(IJobEvent jobEvent, CancellationToken cancellationToken = default);
}
