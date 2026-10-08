using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Notifications;
using BlazorFluent.Core.Dtos;
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
    /// <summary>
    /// Handles background notification operations within an isolated dependency injection scope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>1. Eliminates Blazor Server DbContext Concurrency Crashes</b><br/>
    /// In Blazor InteractiveServer, an entire user session (circuit) shares a single scoped AppDbContext. 
    /// Components like the Bell Icon (<see cref="NotificationBellPopup"/>) live in the main layout shell 
    /// and load or refresh notifications in the background. If the bell icon queries the database at the 
    /// exact same millisecond that a user saves a task, posts a comment, or loads a project board, 
    /// EF Core will crash with a <see cref="System.InvalidOperationException"/> (indicating a second operation 
    /// was started on this context instance before a previous operation completed).
    /// </para>
    /// <para>
    /// By creating a fresh scope via <c>_scopeFactory.CreateScope()</c>, the notification engine gets 
    /// its own dedicated AppDbContext that never collides with the user's active page circuit.
    /// </para>
    /// <para>
    /// <b>2. Protects Long-Running Dispatches (Teams Webhooks &amp; Emails)</b><br/>
    /// The dispatch process does not simply save to the database; it also handles long-running outbound network calls, 
    /// including Teams webhooks via <c>_teamsSender.SendTeamsNotificationAsync</c> and SMTP emails via 
    /// <c>_emailSender.SendEmailNotificationAsync</c>. 
    /// </para>
    /// <para>
    /// Because network calls can take 1 to 5 seconds, holding onto the user's primary DbContext would freeze 
    /// the entire user interface while waiting for the operations to complete. Executing within an isolated 
    /// scope ensures the notification transaction and external dispatches run independently, allowing them 
    /// to complete without blocking the UI or being prematurely cancelled if the user navigates away.
    /// </para>
    /// <para>
    /// <b>3. Elevated System Daemon Authority (SetSystemDaemon)</b><br/>
    /// In BlazorFluent's Zero-Trust architecture, database queries are inspected by interceptors such as 
    /// <c>ZeroTrustDbCommandInterceptor</c> and <c>AuditableEntityInterceptor</c>. 
    /// </para>
    /// <para>
    /// When automated background processes (like <c>TaskCadenceAlertJobHandler</c> or background task creators) 
    /// dispatch alerts, they may operate without an active interactive user. Resolving the <see cref="ICurrentUser"/> 
    /// within the scope and calling <c>SetSystemDaemon("NotificationService")</c> grants the notification engine 
    /// system-level permission to successfully record alerts across tenant boundaries.
    /// </para>
    /// <para>
    /// <b>4. Explicit Multi-Tenant Context Propagation</b><br/>
    /// Because creating a new DI scope generates fresh, uninitialized instances of scoped services, the newly 
    /// created scope's <see cref="ITenantContext"/> begins empty. 
    /// </para>
    /// <para>
    /// This implementation explicitly copies over the caller's active tenant properties (including TenantId, 
    /// TenantName, UserType, AllowedTenants, and IsHost). This ensures that EF Core's global query filters 
    /// remain intact and continue to isolate notification data strictly to the correct tenant.
    /// </para>
    /// </remarks>

    private IServiceScope CreateScopedContext()
    {
        var scope = _scopeFactory.CreateScope();
        var scopedUser = scope.ServiceProvider.GetService<ICurrentUser>();
        scopedUser?.SetSystemDaemon("NotificationService");

        var scopedTenant = scope.ServiceProvider.GetService<ITenantContext>();
        if (scopedTenant != null && !string.IsNullOrWhiteSpace(_tenantContext.TenantId))
        {
            scopedTenant.Initialize(
                _tenantContext.TenantId,
                _tenantContext.TenantName,
                _tenantContext.UserType,
                _tenantContext.AllowedTenants,
                _tenantContext.IsHost);
        }

        return scope;
    }

    public async Task<NotificationEntity> SendAsync(SendNotificationReqDto request, CancellationToken ct = default)
    {
        using var scope = CreateScopedContext();
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
    //REVIEWED-CSK
    public async Task<IReadOnlyList<NotificationEntity>> GetNotificationsAsync(NotificationFilterReqDto filter, CancellationToken ct = default)
    {
        using var scope = CreateScopedContext();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var currentUserId = _currentUser.UserId;
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(db, ProjectRole.ReadOnly, ct);

        var query = db.Notifications
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId && (n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId)));

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
            .Take(filter.Take > 0 ? filter.Take : 50)
            .ToListAsync(ct);
    }

    public async Task<UnreadNotificationCountsRespDto> GetUnreadCountsAsync(CancellationToken ct = default)
    {
        using var scope = CreateScopedContext();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var currentUserId = _currentUser.UserId;
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(db, ProjectRole.ReadOnly, ct);

        var genericCount = await db.Notifications
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId
                && !n.IsRead
                && n.Category == NotificationCategory.Generic
                && n.UserId == null
                && (n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId)))
            .CountAsync(ct);

        var personalCount = 0;
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            personalCount = await db.Notifications
                .AsNoTracking()
                .Where(n => n.TenantId == tenantId
                    && !n.IsRead
                    && n.Category == NotificationCategory.Personal
                    && n.UserId == currentUserId
                    && (n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId)))
                .CountAsync(ct);
        }

        return new UnreadNotificationCountsRespDto
        {
            GenericUnread = genericCount,
            PersonalUnread = personalCount
        };
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        using var scope = CreateScopedContext();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var currentUserId = _currentUser.UserId;
        var notification = await db.Notifications
            .AsTracking()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == notificationId, ct);
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
        using var scope = CreateScopedContext();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var currentUserId = _currentUser.UserId;
        var authorizedProjectIds = await GetAuthorizedProjectIdsAsync(db, ProjectRole.ReadOnly, ct);

        var query = db.Notifications
            .Where(n => n.TenantId == tenantId && !n.IsRead && (n.ProjectId == Guid.Empty || authorizedProjectIds.Contains(n.ProjectId)));

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
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        if (_currentUser.IsRootAdmin)
        {
            return await db.Projects
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        var userId = _currentUser.UserId ?? "anonymous";
        var userRoles = await db.ProjectUserRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.UserId == userId && !r.IsDeleted)
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
