using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskOption : ComponentBase
{
    [CascadingParameter] private ICskSelect? ParentSelect { get; set; }

    [Parameter] public object? Value { get; set; }
    [Parameter] public bool Selected { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    public string ComputedValueString => Value?.ToString() ?? string.Empty;

    public bool IsSelected => Selected || (ParentSelect != null && ParentSelect.IsValueSelected(ComputedValueString));
}
