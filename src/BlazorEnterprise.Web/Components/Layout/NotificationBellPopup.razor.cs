using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Notifications;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Components.Layout;

public partial class NotificationBellPopup : ComponentBase
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private INotifyService MyNotificationService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private ILogger<NotificationBellPopup> Logger { get; set; } = default!;

    private bool _isOpen;
    private bool _isLoading;
    private bool _onlyUnread;
    private NotificationCategoryEnum _activeCategory = NotificationCategoryEnum.Generic;
    private UnreadNotificationCountsRespDto _unreadCounts = new();
    private List<NotificationEntity> _notifications = new();

    protected override async Task OnInitializedAsync()
    {
        if (CurrentUser.IsAuthenticated)
        {
            await LoadUnreadCountsAsync();
        }
    }

    private async Task TogglePanel()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            await RefreshNotificationsAsync();
        }
    }

    private async Task SetCategory(NotificationCategoryEnum category)
    {
        if (_activeCategory == category) return;
        _activeCategory = category;
        await RefreshNotificationsAsync();
    }

    private async Task OnOnlyUnreadChanged(ChangeEventArgs e)
    {
        _onlyUnread = e.Value is bool b && b;
        await RefreshNotificationsAsync();
    }

    private async Task RefreshNotificationsAsync()
    {
        if (!CurrentUser.IsAuthenticated) return;

        _isLoading = true;
        try
        {
            await LoadUnreadCountsAsync();
            var filter = new NotificationFilterReqDto
            {
                Category = _activeCategory,
                OnlyUnread = _onlyUnread,
                Take = 30
            };

            var items = await MyNotificationService.GetNotificationsAsync(filter);
            _notifications = items.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing notifications in NotificationBell");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadUnreadCountsAsync()
    {
        if (!CurrentUser.IsAuthenticated)
        {
            _unreadCounts = new();
            return;
        }

        try
        {
            _unreadCounts = await MyNotificationService.GetUnreadCountsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading unread notification counts");
        }
    }

    private int GetActiveTabUnreadCount() =>
        _activeCategory == NotificationCategoryEnum.Personal
            ? _unreadCounts.PersonalUnread
            : _unreadCounts.GenericUnread;

    private async Task MarkAsReadAsync(Guid notificationId)
    {
        var success = await MyNotificationService.MarkAsReadAsync(notificationId);
        if (success)
        {
            var item = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (item is not null)
            {
                item.IsRead = true;
                item.ReadAtUtc = DateTime.UtcNow;
            }
            await LoadUnreadCountsAsync();
        }
    }

    private async Task MarkAllAsReadAsync()
    {
        await MyNotificationService.MarkAllAsReadAsync(_activeCategory);
        foreach (var item in _notifications)
        {
            item.IsRead = true;
            item.ReadAtUtc = DateTime.UtcNow;
        }
        await LoadUnreadCountsAsync();
    }

    private async Task NavigateToLink(NotificationEntity notification)
    {
        if (!notification.IsRead)
        {
            await MarkAsReadAsync(notification.Id);
        }

        if (!string.IsNullOrWhiteSpace(notification.LinkUrl))
        {
            _isOpen = false;
            NavigationManager.NavigateTo(notification.LinkUrl);
        }
    }

    private string GetRelativeTime(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime.ToUniversalTime();
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return TimeZoneService.FormatDate(dateTime, "MMM dd");
    }
}
