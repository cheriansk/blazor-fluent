using System.Threading.Channels;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Queue;

public class ChannelJobEventQueue : IJobEventQueue, IJobEventPublisher
{
    private readonly Channel<IJobEvent> _channel;
    private readonly ILogger<ChannelJobEventQueue> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    public ChannelJobEventQueue(
        ILogger<ChannelJobEventQueue> logger,
        IServiceScopeFactory? scopeFactory = null,
        int capacity = 1000)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
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

        if (_scopeFactory != null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                // Least Privilege: scope is strictly locked to target tenant or 'system', never IsHost=true
                var tenantContext = scope.ServiceProvider.GetService<ITenantContext>();
                if (tenantContext != null)
                {
                    var targetTenantId = !string.IsNullOrWhiteSpace(jobEvent.TenantId) ? jobEvent.TenantId : "system";
                    tenantContext.Initialize(
                        tenantId: targetTenantId,
                        tenantName: targetTenantId == "system" ? "System Daemon" : null,
                        userType: Core.DataListTypes.UserType.CompanyUser,
                        allowedTenants: [],
                        isHost: false);
                }

                var jobName = !string.IsNullOrWhiteSpace(jobEvent.JobName)
                    ? jobEvent.JobName
                    : jobEvent.GetType().Name.Replace("Event", string.Empty);

                var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
                currentUser?.SetSystemDaemon($"ChannelJob:{jobName}");

                var eventTracker = scope.ServiceProvider.GetService<IEventTrackerService>();
                if (eventTracker != null)
                {
                    await eventTracker.TrackPublishAsync(
                        jobEvent,
                        eventId: jobEvent.EventId,
                        correlationId: jobEvent.CorrelationId,
                        triggerSource: jobEvent.TriggerSource,
                        sourceClass: nameof(ChannelJobEventQueue),
                        sourceMethod: nameof(EnqueueAsync),
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record event publish tracking for event {EventId}", jobEvent.EventId);
            }
        }

        await _channel.Writer.WriteAsync(jobEvent, cancellationToken);
    }

    public ValueTask PublishAsync(IJobEvent jobEvent, CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(jobEvent, cancellationToken);
    }

    public IAsyncEnumerable<IJobEvent> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void Complete()
    {
        _logger.LogInformation("ChannelJobEventQueue writer completed. Draining remaining events.");
        _channel.Writer.TryComplete();
    }
}
