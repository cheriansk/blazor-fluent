using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskProgressBar : ComponentBase
{
    [Parameter] public int Min { get; set; } = 0;
    [Parameter] public int Max { get; set; } = 100;
    [Parameter] public int? Value { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}

public class FluentProgressBar : CskProgressBar
{
}
