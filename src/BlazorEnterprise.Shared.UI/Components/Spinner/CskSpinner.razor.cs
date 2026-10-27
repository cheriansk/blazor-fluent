using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskSpinner : ComponentBase
{
    [Parameter] public CskSpinnerSize Size { get; set; } = CskSpinnerSize.Medium;
    [Parameter] public string? Class { get; set; }
    [Parameter] public string AriaLabel { get; set; } = "Loading...";
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetSizeClass() => Size switch
    {
        CskSpinnerSize.Small => "csk-spinner-sm",
        CskSpinnerSize.Large => "csk-spinner-lg",
        _ => "csk-spinner-md"
    };
}
