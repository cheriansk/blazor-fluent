using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskTabs : ComponentBase
{
    private readonly List<CskTab> _tabs = new();

    [Parameter] public string? ActiveTabId { get; set; }
    [Parameter] public EventCallback<string?> ActiveTabIdChanged { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    internal void RegisterTab(CskTab tab)
    {
        if (!_tabs.Contains(tab))
        {
            _tabs.Add(tab);
            if (string.IsNullOrEmpty(ActiveTabId) && _tabs.Count == 1)
            {
                ActiveTabId = tab.Id;
            }
            StateHasChanged();
        }
    }

    internal void UnregisterTab(CskTab tab)
    {
        if (_tabs.Remove(tab))
        {
            StateHasChanged();
        }
    }

    private bool IsTabActive(CskTab tab)
    {
        if (!string.IsNullOrEmpty(ActiveTabId))
        {
            return string.Equals(ActiveTabId, tab.Id, StringComparison.OrdinalIgnoreCase);
        }
        return _tabs.FirstOrDefault() == tab;
    }

    private CskTab? GetActiveTab()
    {
        if (!string.IsNullOrEmpty(ActiveTabId))
        {
            var found = _tabs.FirstOrDefault(t => string.Equals(t.Id, ActiveTabId, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
        }
        return _tabs.FirstOrDefault();
    }

    private async Task SelectTabAsync(CskTab tab)
    {
        ActiveTabId = tab.Id;
        if (ActiveTabIdChanged.HasDelegate)
        {
            await ActiveTabIdChanged.InvokeAsync(tab.Id);
        }
        StateHasChanged();
    }
}

public class FluentTabs : CskTabs
{
}

public class CskTab : ComponentBase, IDisposable
{
    [CascadingParameter] protected CskTabs? ParentTabs { get; set; }

    [Parameter] public string? Id { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? HeaderTemplate { get; set; }
    [Parameter] public RenderFragment? Content { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void OnInitialized()
    {
        ParentTabs?.RegisterTab(this);
    }

    public void Dispose()
    {
        ParentTabs?.UnregisterTab(this);
        GC.SuppressFinalize(this);
    }
}

public class FluentTab : CskTab
{
}
