using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskSpinner : ComponentBase
{
    [Parameter] public CskSpinnerSize Size { get; set; } = CskSpinnerSize.Medium;
    [Parameter] public CskColor Color { get; set; } = CskColor.Primary;
    [Parameter] public string? Class { get; set; }
    [Parameter] public string AriaLabel { get; set; } = "Loading...";
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetSizeClass() => Size switch
    {
        CskSpinnerSize.Small => "csk-spinner-sm",
        CskSpinnerSize.Large => "csk-spinner-lg",
        _ => "csk-spinner-md"
    };

    private string GetColorClass() => Color switch
    {
        CskColor.Secondary => "csk-color-secondary",
        CskColor.Success => "csk-color-success",
        CskColor.Warning => "csk-color-warning",
        CskColor.Error => "csk-color-error",
        CskColor.Info => "csk-color-info",
        CskColor.Dark => "csk-color-dark",
        _ => "csk-color-primary"
    };
}
