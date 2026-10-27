using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskCard : ComponentBase
{
    [Parameter] public CskCardVariant Variant { get; set; } = CskCardVariant.Elevated;
    [Parameter] public int Elevation { get; set; } = 1;
    [Parameter] public bool Outlined { get; set; }
    [Parameter] public RenderFragment? Header { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }
    [Parameter] public bool Clickable { get; set; } = false;
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private async Task HandleClickAsync(MouseEventArgs e)
    {
        if (Clickable && OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(e);
        }
    }

    private string GetVariantClass()
    {
        if (Outlined) return "csk-card-outlined";
        return Variant switch
        {
            CskCardVariant.Filled => "csk-card-filled",
            CskCardVariant.Outlined => "csk-card-outlined",
            _ => "csk-card-elevated"
        };
    }

    private string GetElevationClass() => Elevation switch
    {
        0 => "csk-elevation-0",
        2 => "csk-elevation-2",
        3 => "csk-elevation-3",
        >= 4 => "csk-elevation-4",
        _ => "csk-elevation-1"
    };
}
