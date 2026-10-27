using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public enum TooltipPosition
{
    Top,
    Bottom,
    Left,
    Right,
    Start,
    End
}

public partial class CskTooltip : ComponentBase
{
    [Parameter] public string? Anchor { get; set; }
    [Parameter] public TooltipPosition Position { get; set; } = TooltipPosition.Bottom;
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}

public class FluentTooltip : CskTooltip
{
}
