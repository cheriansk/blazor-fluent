using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Notifications;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsNotificationsTab : ComponentBase
{
    [Inject] private INotifyService NotificationService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SettingsNotificationsTab> Logger { get; set; } = default!;

    public record FilterOptionItem(string Value, string Label);

    private bool _isLoading;
    private List<NotificationEntity> _notifications = [];
    private int _unreadCount;

    private List<FilterOptionItem> _tenantFilterItems = [];
    private static readonly List<FilterOptionItem> _categoryFilterItems =
    [
        new("", "All Categories"),
        new("Generic", "General Broadcast"),
        new("Personal", "Personal Alerts")
    ];
    private static readonly List<FilterOptionItem> _severityFilterItems =
    [
        new("", "All Severities"),
        new("Info", "Information"),
        new("Success", "Success"),
        new("Warning", "Warning"),
        new("Error", "Error")
    ];
    private static readonly List<FilterOptionItem> _statusFilterItems =
    [
        new("all", "All Statuses"),
        new("unread", "Unread Only"),
        new("read", "Read Only")
    ];

    private string _selectedTenantId = string.Empty;
    private string _selectedCategory = string.Empty;
    private string _selectedSeverity = string.Empty;
    private string _selectedStatus = "all";
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private string _searchText = string.Empty;
    private int _pageSize = 50;

    protected override async Task OnInitializedAsync()
    {
        _tenantFilterItems =
        [
            new("", "All Organizations"),
            .. TenantContext.AllowedTenants.Select(t => new FilterOptionItem(t.Id.ToString(), $"{t.Name} ({t.Id})"))
        ];

        await LoadNotificationsAsync();
    }

    private async Task LoadNotificationsAsync()
    {
        if (!CurrentUser.IsAuthenticated) return;

        _isLoading = true;
        try
        {
            NotificationCategoryEnum? category = _selectedCategory switch
            {
                "Generic" => NotificationCategoryEnum.Generic,
                "Personal" => NotificationCategoryEnum.Personal,
                _ => null
            };

            NotificationSeverityEnum? severity = _selectedSeverity switch
            {
                "Info" => NotificationSeverityEnum.Info,
                "Success" => NotificationSeverityEnum.Success,
                "Warning" => NotificationSeverityEnum.Warning,
                "Error" => NotificationSeverityEnum.Error,
                _ => null
            };

            bool? isRead = _selectedStatus switch
            {
                "unread" => false,
                "read" => true,
                _ => null
            };

            var filter = new NotificationFilterReqDto
            {
                TenantId = Guid.TryParse(_selectedTenantId, out var parsedTenantGuid) ? parsedTenantGuid : null,
                Category = category,
                Severity = severity,
                IsRead = isRead,
                FromDateUtc = _fromDate.HasValue ? DateTime.SpecifyKind(_fromDate.Value.Date, DateTimeKind.Utc) : null,
                ToDateUtc = _toDate.HasValue ? DateTime.SpecifyKind(_toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc) : null,
                SearchTerm = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText,
                Take = _pageSize
            };

            var items = await NotificationService.GetNotificationsAsync(filter);
            _notifications = items.ToList();
            _unreadCount = _notifications.Count(n => !n.IsRead);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading notification history in SettingsNotificationsTab.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ResetFiltersAsync()
    {
        _selectedTenantId = string.Empty;
        _selectedCategory = string.Empty;
        _selectedSeverity = string.Empty;
        _selectedStatus = "all";
        _fromDate = null;
        _toDate = null;
        _searchText = string.Empty;
        await LoadNotificationsAsync();
    }

    private async Task MarkAsReadAsync(NotificationEntity item)
    {
        var success = await NotificationService.MarkAsReadAsync(item.Id);
        if (success)
        {
            item.IsRead = true;
            item.ReadAtUtc = DateTime.UtcNow;
            _unreadCount = _notifications.Count(n => !n.IsRead);
        }
    }

    private async Task MarkAllFilteredAsReadAsync()
    {
        var tenantFilter = string.IsNullOrWhiteSpace(_selectedTenantId) ? null : _selectedTenantId;
        NotificationCategoryEnum? category = _selectedCategory switch
        {
            "Generic" => NotificationCategoryEnum.Generic,
            "Personal" => NotificationCategoryEnum.Personal,
            _ => null
        };

        await NotificationService.MarkAllAsReadAsync(category, tenantFilter);

        var now = DateTime.UtcNow;
        foreach (var item in _notifications.Where(n => !n.IsRead))
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
        }
        _unreadCount = 0;
    }

    private async Task OpenLink(NotificationEntity item)
    {
        if (!item.IsRead)
        {
            await MarkAsReadAsync(item);
        }

        if (!string.IsNullOrWhiteSpace(item.LinkUrl))
        {
            NavigationManager.NavigateTo(item.LinkUrl);
        }
    }
}
