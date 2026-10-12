namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Circuit-scoped in-memory state tracking the currently selected or initially requested tab
/// in the centralized Settings page without modifying or exposing query parameters in the browser URL.
/// </summary>
public interface ISettingsNavigationState
{
    /// <summary>
    /// Gets or sets the target tab identifier to open when navigating to the Settings page.
    /// Cleared once consumed by the Settings page.
    /// </summary>
    string? InitialTab { get; set; }

    /// <summary>
    /// Sets the target tab in-memory and navigates to "/settings" with a clean URL (no query strings).
    /// </summary>
    /// <param name="tabId">The target tab identifier ("tenants", "projects", "jobs", "events", "sessions").</param>
    void NavigateToTab(string tabId);
}
