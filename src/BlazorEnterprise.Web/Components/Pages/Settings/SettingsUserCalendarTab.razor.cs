using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsUserCalendarTab : ComponentBase
{
    [Inject] private ICalendarService CalendarService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ILogger<SettingsUserCalendarTab> Logger { get; set; } = default!;

private bool _isLoading = true;
    private bool _isSaving;
    private string? _statusMessage;
    private bool _isError;
    private string? _modalError;
    private DateTime _selectedDate = DateTime.UtcNow.Date;
    private IReadOnlyList<CalendarItemDto> _calendarItems = [];

    // Modal state
    private bool _showEventModal;
    private string _newEventTitle = string.Empty;
    private string _newEventDescription = string.Empty;
    private string _newLocation = string.Empty;
    private DateTime? _newEventDate = DateTime.UtcNow.Date;
    private CalendarEntryTypeEnum _newEntryType = CalendarEntryTypeEnum.General;

    protected override async Task OnInitializedAsync()
    {
        await LoadCalendarAsync();
    }

    private async Task LoadCalendarAsync()
    {
        _isLoading = true;
        try
        {
            var utcToday = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            var start = utcToday.AddMonths(-2);
            var end = utcToday.AddMonths(4);

            _calendarItems = await CalendarService.GetUserCalendarAsync(TenantContext.TenantId, CurrentUser.UserId, start, end);
        }
        catch (Exception ex)
        {
            _statusMessage = $"Failed to load schedule: {ex.Message}";
            _isError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OpenNewEventModal()
    {
        _newEventTitle = string.Empty;
        _newEventDescription = string.Empty;
        _newLocation = string.Empty;
        _newEventDate = _selectedDate;
        _newEntryType = CalendarEntryTypeEnum.General;
        _showEventModal = true;
    }

    private void HandleAddEntryForDate(DateTime date)
    {
        _selectedDate = date;
        _newEventDate = date;
        OpenNewEventModal();
    }

    private void HandleItemClick(CalendarItemDto item)
    {
        _statusMessage = $"Selected: {item.Title} ({item.ItemCategory}) - Due: {item.EndUtc:yyyy-MM-dd}";
        _isError = false;
    }

    private async Task SaveNewEventAsync()
    {
        if (string.IsNullOrWhiteSpace(_newEventTitle))
        {
            _modalError = "Event title is required.";
            return;
        }

        _isSaving = true;
        try
        {
            var tenantId = TenantContext.TenantId;
            Guid.TryParse(CurrentUser.UserId, out var userGuid);
            var eventDate = DateTime.SpecifyKind((_newEventDate ?? DateTime.UtcNow.Date).Date, DateTimeKind.Utc);

            var baseDesc = _newEventDescription?.Trim() ?? string.Empty;
            var description = !string.IsNullOrWhiteSpace(_newLocation)
                ? (string.IsNullOrWhiteSpace(baseDesc) ? $"Location: {_newLocation.Trim()}" : $"{baseDesc}\nLocation: {_newLocation.Trim()}")
                : baseDesc;

            var entry = new UserCalendarEntity
            {
                TenantId = tenantId,
                UserId = userGuid,
                Title = _newEventTitle.Trim(),
                Description = description,
                StartDate = eventDate,
                EndDate = eventDate.AddHours(1),
                EntryType = _newEntryType,
                Status = CalendarEntryStatusEnum.Confirmed
            };

            var result = await CalendarService.CreateUserCalendarEntryAsync(entry);
            if (result.IsSuccess)
            {
                _statusMessage = "Personal appointment scheduled.";
                _isError = false;
                _showEventModal = false;
                await LoadCalendarAsync();
            }
            else
            {
                _modalError = result.Error ?? "Failed to save appointment.";
            }
        }
        catch (Exception ex)
        {
            _modalError = $"Error creating event: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }
}