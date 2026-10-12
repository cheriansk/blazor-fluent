using System.Threading.Channels;
using BlazorFluent.Core.Dtos;

namespace BlazorFluent.Infrastructure.Notifications;

/// <summary>
/// High-performance, zero-allocation bounded in-memory queue using System.Threading.Channels.
/// Decouples UI requests from background notification delivery (email/Teams dispatches).
/// </summary>
public class NotificationChannelQueue
{
    private readonly Channel<SendNotificationReqDto> _channel;

    public NotificationChannelQueue()
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<SendNotificationReqDto>(options);
    }

    /// <summary>
    /// Asynchronously enqueues a notification dispatch request into the channel.
    /// </summary>
    public ValueTask EnqueueAsync(SendNotificationReqDto request, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Attempts non-blocking synchronous enqueue. Returns false if channel buffer is full.
    /// </summary>
    public bool TryEnqueue(SendNotificationReqDto request)
    {
        return _channel.Writer.TryWrite(request);
    }

    /// <summary>
    /// Reads all queued notification requests asynchronously for background consumption.
    /// </summary>
    public IAsyncEnumerable<SendNotificationReqDto> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
