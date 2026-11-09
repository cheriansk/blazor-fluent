using System.Text.RegularExpressions;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Common;

public partial class CskTenantUserPicker : ComponentBase
{
    private readonly string _inputId = $"tenant-user-picker-{Guid.NewGuid():N}";
    private bool _isOpen;
    private bool _isLoading = true;
    private string _searchTerm = string.Empty;
    private List<UserEntity> _tenantUsers = [];
    private string? _validationError;

    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? HelperText { get; set; }
    [Parameter] public string? ErrorText { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool MultiSelect { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(EffectiveErrorText);

    public string? EffectiveErrorText => !string.IsNullOrEmpty(ErrorText) ? ErrorText : _validationError;

    private string DisplayValue => Value ?? string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadTenantUsersAsync();
    }

    private async Task LoadTenantUsersAsync()
    {
        try
        {
            _isLoading = true;
            if (TenantContext.HasActiveTenant)
            {
                var users = await TenantService.GetTenantUsersByIdAsync(TenantContext.TenantId);
                _tenantUsers = users.Where(u => u.IsActive).ToList();
            }
        }
        catch
        {
            _tenantUsers = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private IEnumerable<UserEntity> FilteredUsers
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_searchTerm))
            {
                return _tenantUsers;
            }

            return _tenantUsers.Where(u =>
                (u.FullName?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (u.Email?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
        }
    }

    private bool IsUserSelected(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(Value))
            return false;

        if (MultiSelect)
        {
            var parts = Value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Any(p => string.Equals(p, email, StringComparison.OrdinalIgnoreCase));
        }

        return string.Equals(Value.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task SelectUser(UserEntity user)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;

        if (MultiSelect)
        {
            var currentParts = (Value ?? string.Empty)
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (currentParts.Any(p => string.Equals(p, user.Email, StringComparison.OrdinalIgnoreCase)))
            {
                currentParts.RemoveAll(p => string.Equals(p, user.Email, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                currentParts.Add(user.Email.Trim().ToLowerInvariant());
            }

            var newValue = string.Join("; ", currentParts);
            Value = newValue;
            ValidateEmails(newValue);
            await ValueChanged.InvokeAsync(newValue);
        }
        else
        {
            var newValue = user.Email.Trim().ToLowerInvariant();
            Value = newValue;
            _isOpen = false;
            _searchTerm = string.Empty;
            ValidateEmails(newValue);
            await ValueChanged.InvokeAsync(newValue);
        }
    }

    private async Task OnInputChanged(ChangeEventArgs e)
    {
        var input = e.Value?.ToString() ?? string.Empty;
        Value = input;
        _searchTerm = MultiSelect
            ? input.Split(new[] { ';', ',' }, StringSplitOptions.TrimEntries).LastOrDefault() ?? string.Empty
            : input;

        _isOpen = true;
        ValidateEmails(input);
        await ValueChanged.InvokeAsync(input);
    }

    private void OnInputFocused()
    {
        if (!Disabled && !ReadOnly)
        {
            _searchTerm = string.Empty;
            _isOpen = true;
        }
    }

    private void OnInputBlur()
    {
        // Give time for click inside dropdown to execute
        Task.Delay(200).ContinueWith(_ =>
        {
            _isOpen = false;
            ValidateEmails(Value);
            InvokeAsync(StateHasChanged);
        });
    }

    private void ToggleDropdown()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            _searchTerm = string.Empty;
        }
    }

    private void ValidateEmails(string? val)
    {
        _validationError = null;
        if (string.IsNullOrWhiteSpace(val))
        {
            if (Required)
            {
                _validationError = "This field is required.";
            }
            return;
        }

        var emails = val.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var invalidEmails = new List<string>();

        foreach (var email in emails)
        {
            var exists = _tenantUsers.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                invalidEmails.Add(email);
            }
        }

        if (invalidEmails.Count > 0)
        {
            _validationError = invalidEmails.Count == 1
                ? $"User '{invalidEmails[0]}' is not a member of this tenant."
                : $"Users [{string.Join(", ", invalidEmails)}] are not members of this tenant.";
        }
    }
}
