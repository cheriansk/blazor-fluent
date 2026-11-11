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
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

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
        await LoadEventsAsync();
    }

    private async Task LoadEventsAsync()
    {
        _isLoading = true;
        try
        {
            if (TenantContext.HasActiveTenant && TenantContext.TenantId != Guid.Empty)
            {
                _filter.TenantId = TenantContext.TenantId.ToString();
            }
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
        _filter = new EventTrackerFilterReqDto { PageSize = 20, TenantId = TenantContext.HasActiveTenant ? TenantContext.TenantId.ToString() : null };
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

    private async Task FilterByCorrelationAsync(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return;
        _filter.SearchTerm = correlationId;
        _filter.PageNumber = 1;
        await LoadEventsAsync();
    }

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
    }
}
