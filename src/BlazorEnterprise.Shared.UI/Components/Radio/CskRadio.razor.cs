using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskRadio : ComponentBase
{
    [CascadingParameter] private ICskRadioGroup? ParentGroup { get; set; }

    [Parameter] public object? Value { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    public bool IsSelected => ParentGroup != null && ParentGroup.IsValueSelected(Value);
    public bool IsDisabled => Disabled || (ParentGroup != null && ParentGroup.IsDisabled);

    private async Task HandleClickAsync()
    {
        if (IsDisabled || ParentGroup == null) return;
        await ParentGroup.SelectValueAsync(Value);
    }
}
