using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Shared;

/// <summary>
/// Reusable lazy-loading multi-tab container component wrapping CskTabs from BlazorEnterprise.Shared.UI.
/// </summary>
public partial class TabContainerComp : ComponentBase
{
    [Parameter]
    public IReadOnlyList<TabItemModel> Tabs { get; set; } = Array.Empty<TabItemModel>();

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public string? DefaultActiveTabId { get; set; }

    [Parameter]
    public EventCallback<string?> ActiveTabIdChanged { get; set; }

    private string? _activeTabId;
    private readonly HashSet<string> _loadedTabIds = new(StringComparer.OrdinalIgnoreCase);

    public string? ActiveTabId
    {
        get => _activeTabId;
        set
        {
            if (!string.Equals(_activeTabId, value, StringComparison.OrdinalIgnoreCase))
            {
                _activeTabId = value;
                if (!string.IsNullOrEmpty(value))
                {
                    _loadedTabIds.Add(value);
                }
                _ = ActiveTabIdChanged.InvokeAsync(value);
            }
        }
    }

    protected override void OnInitialized()
    {
        if (!string.IsNullOrEmpty(DefaultActiveTabId))
        {
            _activeTabId = DefaultActiveTabId;
            _loadedTabIds.Add(DefaultActiveTabId);
        }
        else if (Tabs.Count > 0)
        {
            _activeTabId = Tabs[0].Id;
            _loadedTabIds.Add(Tabs[0].Id);
        }
    }

    protected override void OnParametersSet()
    {
        if (string.IsNullOrEmpty(_activeTabId) && Tabs.Count > 0)
        {
            _activeTabId = Tabs[0].Id;
            _loadedTabIds.Add(Tabs[0].Id);
        }
        else if (!string.IsNullOrEmpty(_activeTabId) && !_loadedTabIds.Contains(_activeTabId))
        {
            _loadedTabIds.Add(_activeTabId);
        }
    }

    private bool IsTabLoaded(string tabId) => _loadedTabIds.Contains(tabId);
}
