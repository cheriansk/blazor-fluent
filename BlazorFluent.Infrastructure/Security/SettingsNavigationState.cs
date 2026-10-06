using BlazorFluent.Core.Contracts;
using Microsoft.AspNetCore.Components;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Circuit-scoped implementation of <see cref="ISettingsNavigationState"/> managing in-memory tab state
/// for the Settings page and navigating without appending query parameters to the browser address bar.
/// </summary>
public class SettingsNavigationState : ISettingsNavigationState
{
    private readonly NavigationManager _navigationManager;

    public SettingsNavigationState(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public string? InitialTab { get; set; }

    public void NavigateToTab(string tabId)
    {
        InitialTab = tabId;
        _navigationManager.NavigateTo("/settings");
    }
}
