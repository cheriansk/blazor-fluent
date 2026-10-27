using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskDialog : ComponentBase
{
    private bool _isOpen;

    [Parameter] public bool Modal { get; set; } = true;
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? TitleTemplate { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? ActionTemplate { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    public Task ShowAsync()
    {
        _isOpen = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        _isOpen = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleBackdropClick()
    {
        if (!Modal)
        {
            await HideAsync();
        }
    }
}
