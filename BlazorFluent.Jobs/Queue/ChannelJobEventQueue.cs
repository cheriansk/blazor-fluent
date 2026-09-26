using System.Threading.Channels;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Queue;

public class ChannelJobEventQueue : IJobEventQueue
{
    private readonly Channel<IJobEvent> _channel;
    private readonly ILogger<ChannelJobEventQueue> _logger;

    public ChannelJobEventQueue(ILogger<ChannelJobEventQueue> logger, int capacity = 1000)
    {
        _logger = logger;
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        };
        _channel = Channel.CreateBounded<IJobEvent>(options);
    }

    public async ValueTask EnqueueAsync(IJobEvent jobEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobEvent);

        _logger.LogInformation("Enqueuing job event {EventId} of type {EventType} from source {Source}",
            jobEvent.EventId, jobEvent.GetType().Name, jobEvent.TriggerSource);

        await _channel.Writer.WriteAsync(jobEvent, cancellationToken);
    }

    public IAsyncEnumerable<IJobEvent> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
