using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Events;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Shared.Contracts.Common;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsEventsTab : ComponentBase, IDisposable
{
    [Inject] private IEventTrackerService EventTrackerService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IRootAdminService RootAdmin { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    public record TenantFilterOption(string Id, string Name);

    private List<TenantFilterOption> _availableTenants = new();
    private string? _selectedTenantId;
    private string _selectedTenantName = string.Empty;

    private EventTrackerFilterReqDto _filter = new() { PageSize = 20 };
    private PagedResultRespDto<EventPublishTrackerEntity>? _eventResult;
    private EventPublishTrackerEntity? _selectedEvent;
    private bool _isLoading;
    private bool _copied;
    private string _inGridSearchText = string.Empty;

    private readonly List<string> _statuses = new() { "All", "Published", "InProgress", "Completed", "PartiallyFailed", "Failed" };
    private string _selectedStatus = "All";

    protected override async Task OnInitializedAsync()
    {
        TimeZoneService.OnChange += HandleTimeZoneChanged;
        await LoadAvailableTenantsAsync();
    }

    private async Task LoadAvailableTenantsAsync()
    {
        var isRootAdmin = CurrentUser.IsRootAdmin;
        if (isRootAdmin)
        {
            var dbTenants = (await TenantService.GetAllTenantsAsync())
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(
                    t.Slug,
                    t.Slug == RootAdmin.RootTenantSlug ? $"{t.Name} (Default Root)" : t.Name))
                .ToList();

            _availableTenants = dbTenants;
        }
        else
        {
            _availableTenants = TenantContext.AllowedTenants
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(t.Id.ToString(), t.Name))
                .ToList();
        }

        if (_availableTenants.Count == 1)
        {
            await OnTenantSelected(_availableTenants[0].Id);
        }
    }

    private async Task OnTenantSelected(string? tenantId)
    {
        _selectedTenantId = tenantId;
        _selectedTenantName = _availableTenants.FirstOrDefault(t => t.Id == tenantId)?.Name ?? tenantId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_selectedTenantId))
        {
            _filter.TenantId = _selectedTenantId;
            _filter.PageNumber = 1;
            await LoadEventsAsync();
        }
        else
        {
            _eventResult = null;
            _selectedEvent = null;
        }
    }

    private void ClearTenantFilter()
    {
        _selectedTenantId = null;
        _selectedTenantName = string.Empty;
        _filter.TenantId = null;
        _eventResult = null;
        _selectedEvent = null;
    }

    private async Task LoadEventsAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        _isLoading = true;
        try
        {
            _filter.TenantId = _selectedTenantId;
            _eventResult = await EventTrackerService.GetEventsAsync(_filter);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private IQueryable<EventPublishTrackerEntity> FilteredEventItems
    {
        get
        {
            if (_eventResult is null) return Enumerable.Empty<EventPublishTrackerEntity>().AsQueryable();

            var query = _eventResult.Items.AsQueryable();
            if (!string.IsNullOrWhiteSpace(_inGridSearchText))
            {
                var term = _inGridSearchText.Trim();
                query = query.Where(e =>
                    e.EventName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    e.GetEffectiveSenderOrigin().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    e.GetEffectiveSender().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    e.SourceClass.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    e.SourceMethod.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (e.UserEmail != null && e.UserEmail.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (e.UserId != null && e.UserId.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (e.CorrelationId != null && e.CorrelationId.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }
            return query;
        }
    }

    private async Task SelectEventAsync(EventPublishTrackerEntity entity)
    {
        _selectedEvent = entity;
        _copied = false;
        var freshConsumptions = await EventTrackerService.GetConsumptionsAsync(entity.Id);
        if (freshConsumptions != null)
        {
            _selectedEvent.Consumptions = freshConsumptions.ToList();
        }
    }

    private async Task CopyPayloadAsync()
    {
        if (_selectedEvent != null && !string.IsNullOrWhiteSpace(_selectedEvent.PayloadJson))
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", _selectedEvent.PayloadJson);
            _copied = true;
            StateHasChanged();
        }
    }

    private void OnStatusChanged(string? value)
    {
        _selectedStatus = value ?? "All";
        var parsedStatus = _selectedStatus.ToEnumOrNull<EventTrackerStatusEnum>();
        if (_selectedStatus == "All" || parsedStatus == null)
        {
            _filter.Status = null;
        }
        else
        {
            _filter.Status = parsedStatus;
        }
    }

    private async Task SearchAsync()
    {
        _filter.PageNumber = 1;
        await LoadEventsAsync();
    }

    private async Task ResetFiltersAsync()
    {
        _filter = new EventTrackerFilterReqDto { PageSize = 20, TenantId = _selectedTenantId };
        _selectedStatus = "All";
        _inGridSearchText = string.Empty;
        await LoadEventsAsync();
    }

    private async Task PreviousPageAsync()
    {
        if (_eventResult?.HasPreviousPage == true)
        {
            _filter.PageNumber--;
            await LoadEventsAsync();
        }
    }

    private async Task NextPageAsync()
    {
        if (_eventResult?.HasNextPage == true)
        {
            _filter.PageNumber++;
            await LoadEventsAsync();
        }
    }

    private static BadgeColor GetStatusBadgeColor(EventTrackerStatusEnum status) => status switch
    {
        EventTrackerStatusEnum.Completed => BadgeColor.Success,
        EventTrackerStatusEnum.InProgress => BadgeColor.Warning,
        EventTrackerStatusEnum.Failed => BadgeColor.Severe,
        EventTrackerStatusEnum.PartiallyFailed => BadgeColor.Warning,
        _ => BadgeColor.Informative
    };

    private static string Truncate(string val, int maxLen) =>
        string.IsNullOrEmpty(val) || val.Length <= maxLen ? val : val[..maxLen] + "...";

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
    }
}
