using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Components.Layout;

public partial class CalendarQuickViewPopup : ComponentBase
{
    [Inject] private ICalendarService CalendarService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<CalendarQuickViewPopup> Logger { get; set; } = default!;

    private bool _isOpen;
    private bool _isLoading = true;
    private string? _errorMessage;
    private DateTime _selectedDate = DateTime.UtcNow.Date;
    private IReadOnlyList<CalendarItemDto> _calendarItems = Array.Empty<CalendarItemDto>();

    private async Task ToggleModal()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            await LoadCalendarItemsAsync();
        }
    }

    private void CloseModal()
    {
        _isOpen = false;
        _errorMessage = null;
    }

    private void OpenFullSchedule()
    {
        _isOpen = false;
        NavigationManager.NavigateTo("/settings?tab=calendar");
    }

    private async Task LoadCalendarItemsAsync()
    {
        if (!TenantContext.HasActiveTenant || TenantContext.TenantId == Guid.Empty || TenantContext.IsRootTenant)
        {
            _isLoading = false;
            return;
        }

        try
        {
            _isLoading = true;
            _errorMessage = null;

            var utcToday = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            var start = utcToday.AddMonths(-2);
            var end = utcToday.AddMonths(4);

            _calendarItems = await CalendarService.GetUserCalendarAsync(TenantContext.TenantId, CurrentUser.UserId, start, end);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load user calendar items for quick view.");
            _errorMessage = "Failed to load calendar schedule.";
        }
        finally
        {
            _isLoading = false;
        }
    }
}
