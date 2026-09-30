using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Notifications;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class NotificationService : INotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ITeamsNotificationSender _teamsSender;
    private readonly IEmailNotificationSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IServiceScopeFactory scopeFactory,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ITeamsNotificationSender teamsSender,
        IEmailNotificationSender emailSender,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _teamsSender = teamsSender;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<NotificationEntity> SendAsync(SendNotificationRequest request, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantId = _tenantContext.TenantId ?? "default";

        var notification = new NotificationEntity
        {
            TenantId = tenantId,
            ProjectId = request.ProjectId,
            UserId = request.Category == NotificationCategory.Personal ? request.UserId : null,
            Category = request.Category,
            Severity = request.Severity,
            Title = request.Title,
            Message = request.Message,
            LinkUrl = request.LinkUrl,
            MetadataJson = request.MetadataJson,
            IsRead = false
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        // 1. Teams Dispatch (if requested and configured)
        if ((request.Channels & NotificationChannels.Teams) != 0)
        {
            var webhookUrl = ResolveTeamsWebhook(request.ProjectId);
            if (!string.IsNullOrWhiteSpace(webhookUrl))
            {
                var success = await _teamsSender.SendTeamsNotificationAsync(notification, webhookUrl, ct);
                if (success)
                {
                    notification.SentToTeams = true;
                    notification.TeamsSentAtUtc = DateTime.UtcNow;
                }
            }
        }

        // 2. Email Dispatch (if requested and configured)
        if ((request.Channels & NotificationChannels.Email) != 0)
        {
            var recipientMailbox = ResolveProjectMailbox(request.ProjectId);
            if (!string.IsNullOrWhiteSpace(recipientMailbox))
            {
                var success = await _emailSender.SendEmailNotificationAsync(notification, recipientMailbox, ct);
                if (success)
                {
                    notification.SentToMailbox = true;
                    notification.MailboxSentAtUtc = DateTime.UtcNow;
                }
            }
        }

        if (notification.SentToTeams || notification.SentToMailbox)
        {
            await db.SaveChangesAsync(ct);
        }

        return notification;
    }

    public async Task<IReadOnlyList<NotificationEntity>> GetNotificationsAsync(NotificationFilterRequest filter, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var currentUserId = _currentUser.UserId;
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(db, ProjectRole.ReadOnly, ct);

        var query = db.Notifications
            .AsNoTracking()
            .Where(n => n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId));

        if (filter.ProjectId.HasValue && filter.ProjectId.Value != Guid.Empty)
        {
            query = query.Where(n => n.ProjectId == filter.ProjectId.Value);
        }

        if (filter.Category == NotificationCategory.Personal)
        {
            // Strict personal privacy: only see records specifically designated for current user
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return [];
            }
            query = query.Where(n => n.Category == NotificationCategory.Personal && n.UserId == currentUserId);
        }
        else
        {
            // Generic notifications broadcast to all project/tenant members
            query = query.Where(n => n.Category == NotificationCategory.Generic && n.UserId == null);
        }

        if (filter.OnlyUnread)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.Created)
            .Take(filter.Take > 0 ? filter.Take : 30)
            .ToListAsync(ct);
    }

    public async Task<UnreadNotificationCounts> GetUnreadCountsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var currentUserId = _currentUser.UserId;
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(db, ProjectRole.ReadOnly, ct);

        var genericCount = await db.Notifications
            .AsNoTracking()
            .Where(n => !n.IsRead
                && n.Category == NotificationCategory.Generic
                && n.UserId == null
                && (n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId)))
            .CountAsync(ct);

        var personalCount = 0;
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            personalCount = await db.Notifications
                .AsNoTracking()
                .Where(n => !n.IsRead
                    && n.Category == NotificationCategory.Personal
                    && n.UserId == currentUserId
                    && (n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId)))
                .CountAsync(ct);
        }

        return new UnreadNotificationCounts
        {
            GenericUnread = genericCount,
            PersonalUnread = personalCount
        };
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var currentUserId = _currentUser.UserId;
        var notification = await db.Notifications
            .AsTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId, ct);
        if (notification is null) return false;

        // Privacy check: Personal notification can only be read by recipient
        if (notification.Category == NotificationCategory.Personal && notification.UserId != currentUserId)
        {
            _logger.LogWarning("Security violation: User '{UserId}' attempted to mark personal notification '{NotificationId}' belonging to '{Owner}' as read.",
                currentUserId, notificationId, notification.UserId);
            return false;
        }

        // Project check: Generic notification on a project requires read access
        if (notification.ProjectId != Guid.Empty)
        {
            var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(db, ProjectRole.ReadOnly, ct);
            if (!authorizedProjectIds.Contains(notification.ProjectId))
            {
                return false;
            }
        }

        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(NotificationCategory category, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var currentUserId = _currentUser.UserId;
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(db, ProjectRole.ReadOnly, ct);

        var query = db.Notifications
            .Where(n => !n.IsRead && (n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId)));

        if (category == NotificationCategory.Personal)
        {
            if (string.IsNullOrWhiteSpace(currentUserId)) return 0;
            query = query.Where(n => n.Category == NotificationCategory.Personal && n.UserId == currentUserId);
        }
        else
        {
            query = query.Where(n => n.Category == NotificationCategory.Generic && n.UserId == null);
        }

        var unreadNotifications = await query.ToListAsync(ct);
        if (unreadNotifications.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var item in unreadNotifications)
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        return unreadNotifications.Count;
    }

    private async Task<IReadOnlyList<Guid>> GetAuthorizedProjectIdsAsync(AppDbContext db, ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default)
    {
        if (_tenantContext.IsHost || _currentUser.IsInRole("Admin"))
        {
            return await db.Projects
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        var userId = _currentUser.UserId ?? "anonymous";
        var userRoles = await db.ProjectUserRoles
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .ToListAsync(ct);

        return userRoles
            .Where(r => r.Role.Satisfies(minRole))
            .Select(r => r.ProjectId)
            .Distinct()
            .ToList();
    }

    private string? ResolveTeamsWebhook(Guid projectId)
    {
        var isEnabled = _configuration.GetValue<bool>("Notifications:Teams:Enabled");
        if (!isEnabled) return null;

        if (projectId != Guid.Empty)
        {
            var projectWebhook = _configuration[$"Notifications:Teams:ProjectWebhooks:{projectId}"];
            if (!string.IsNullOrWhiteSpace(projectWebhook)) return projectWebhook;
        }

        return _configuration["Notifications:Teams:DefaultWebhookUrl"];
    }

    private string? ResolveProjectMailbox(Guid projectId)
    {
        var isEnabled = _configuration.GetValue<bool>("Notifications:Email:Enabled");
        if (!isEnabled) return null;

        if (projectId != Guid.Empty)
        {
            var projectMailbox = _configuration[$"Notifications:Email:ProjectMailboxes:{projectId}"];
            if (!string.IsNullOrWhiteSpace(projectMailbox)) return projectMailbox;
        }

        return _configuration["Notifications:Email:DefaultMailbox"];
    }
}
