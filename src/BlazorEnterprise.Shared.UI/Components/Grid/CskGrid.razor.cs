using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskGrid : ComponentBase
{
    [Parameter] public int Spacing { get; set; } = 3;
    [Parameter] public CskJustify Justify { get; set; } = CskJustify.FlexStart;
    [Parameter] public bool AdaptiveRendering { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetJustifyClass() => Justify switch
    {
        CskJustify.Center => "csk-justify-center",
        CskJustify.FlexEnd => "csk-justify-end",
        CskJustify.SpaceBetween => "csk-justify-space-between",
        CskJustify.SpaceAround => "csk-justify-space-around",
        CskJustify.SpaceEvenly => "csk-justify-space-evenly",
        _ => "csk-justify-start"
    };
}
