using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsUserTab : ComponentBase, IDisposable
{
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUnsavedChangesStateService UnsavedChanges { get; set; } = default!;

private const string UnsavedKey = "settings_user_timezone";
    private List<TimeZoneInfo> _availableTimeZones = new();
    private string _selectedTimeZoneId = "UTC";
    private string? _initialTimeZoneId;
    private bool _hasUnsavedChanges;
    private bool _saving;
    private string? _statusMessage;
    private bool _isError;
    private System.Threading.Timer? _clockTimer;

    private string _previewLocalTime = string.Empty;
    private string _previewUtcTime = string.Empty;
    private string _previewZoneDetails = string.Empty;

    protected override void OnInitialized()
    {
        _availableTimeZones = TimeZoneService.AvailableTimeZones.ToList();
        _selectedTimeZoneId = TimeZoneService.TimeZoneId;
        _initialTimeZoneId = _selectedTimeZoneId;
        UpdateLivePreviews();

        _clockTimer = new System.Threading.Timer(_ =>
        {
            InvokeAsync(() =>
            {
                UpdateLivePreviews();
                StateHasChanged();
            });
        }, null, 1000, 1000);
    }

    private void UpdateLivePreviews()
    {
        var nowUtc = DateTime.UtcNow;
        _previewUtcTime = nowUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(_selectedTimeZoneId);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            _previewLocalTime = localTime.ToString("yyyy-MM-dd HH:mm:ss");
            var offset = tz.GetUtcOffset(nowUtc);
            var offsetStr = (offset >= TimeSpan.Zero ? "+" : "-") + offset.ToString(@"hh\:mm");
            _previewZoneDetails = $"{tz.StandardName} (UTC {offsetStr})";
        }
        catch
        {
            _previewLocalTime = nowUtc.ToString("yyyy-MM-dd HH:mm:ss");
            _previewZoneDetails = "UTC (+00:00)";
        }
    }

    private void OnTimeZoneChanged(string? newTzId)
    {
        if (string.IsNullOrWhiteSpace(newTzId)) return;
        _selectedTimeZoneId = newTzId;
        _hasUnsavedChanges = !string.Equals(_selectedTimeZoneId, _initialTimeZoneId, StringComparison.OrdinalIgnoreCase);

        if (_hasUnsavedChanges)
        {
            UnsavedChanges.RegisterChange(UnsavedKey, $"User Timezone changed to '{_selectedTimeZoneId}'");
        }
        else
        {
            UnsavedChanges.ClearChange(UnsavedKey);
        }

        UpdateLivePreviews();
    }

    private async Task SaveTimeZoneAsync()
    {
        _saving = true;
        _statusMessage = null;

        try
        {
            await TimeZoneService.SetTimeZoneAsync(_selectedTimeZoneId);
            _initialTimeZoneId = _selectedTimeZoneId;
            _hasUnsavedChanges = false;
            UnsavedChanges.ClearChange(UnsavedKey);

            _isError = false;
            _statusMessage = $"Timezone successfully updated to '{_selectedTimeZoneId}'. All timestamps across the application now reflect this setting.";
        }
        catch (Exception ex)
        {
            _isError = true;
            _statusMessage = $"Failed to save timezone: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }

    private void ResetToUtcAsync()
    {
        OnTimeZoneChanged("UTC");
    }

    public void Dispose()
    {
        _clockTimer?.Dispose();
        UnsavedChanges.ClearChange(UnsavedKey);
    }
}