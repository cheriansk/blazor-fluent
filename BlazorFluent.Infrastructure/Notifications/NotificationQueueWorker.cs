using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Notifications;

/// <summary>
/// Background worker process consuming notifications from NotificationChannelQueue.
/// Prevents main HTTP request threads from blocking on outbound SMTP or Teams Webhook dispatches.
/// Uses IServiceScopeFactory to safely consume scoped notification sender services.
/// </summary>
public class NotificationQueueWorker : BackgroundService
{
    private readonly NotificationChannelQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationQueueWorker> _logger;

    public NotificationQueueWorker(
        NotificationChannelQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Channel Queue Worker started listening for background dispatches.");

        await foreach (var request in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailNotificationSender>();
                var teamsSender = scope.ServiceProvider.GetRequiredService<ITeamsNotificationSender>();
                var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();

                var tempNotification = new NotificationEntity
                {
                    Title = request.Title,
                    Message = request.Message,
                    Category = request.Category,
                    Severity = request.Severity,
                    LinkUrl = request.LinkUrl,
                    ProjectId = request.ProjectId,
                    UserId = request.UserId
                };

                _logger.LogDebug("Processing background queued notification '{Title}'", request.Title);

                // 1. Teams dispatch
                if ((request.Channels & NotificationChannels.Teams) != 0)
                {
                    var webhookUrl = config[$"Notifications:Teams:ProjectWebhooks:{request.ProjectId}"]
                                     ?? config["Notifications:Teams:DefaultWebhookUrl"];
                    if (!string.IsNullOrWhiteSpace(webhookUrl))
                    {
                        await teamsSender.SendTeamsNotificationAsync(tempNotification, webhookUrl, stoppingToken);
                    }
                }

                // 2. Email dispatch
                if ((request.Channels & NotificationChannels.Email) != 0)
                {
                    var mailbox = config[$"Notifications:Email:ProjectMailboxes:{request.ProjectId}"]
                                  ?? config["Notifications:Email:DefaultMailbox"];
                    if (!string.IsNullOrWhiteSpace(mailbox))
                    {
                        await emailSender.SendEmailNotificationAsync(tempNotification, mailbox, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing queued background notification '{Title}'", request.Title);
            }
        }

        _logger.LogInformation("Notification Channel Queue Worker shutting down cleanly.");
    }
}
