using System.Text;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskGridItem : ComponentBase
{
    [Parameter] public int? xs { get; set; }
    [Parameter] public int? sm { get; set; }
    [Parameter] public int? md { get; set; }
    [Parameter] public int? lg { get; set; }
    [Parameter] public int? xl { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetColumnClasses()
    {
        var sb = new StringBuilder();
        if (xs.HasValue) sb.Append($"csk-col-xs-{xs.Value} ");
        if (sm.HasValue) sb.Append($"csk-col-sm-{sm.Value} ");
        if (md.HasValue) sb.Append($"csk-col-md-{md.Value} ");
        if (lg.HasValue) sb.Append($"csk-col-lg-{lg.Value} ");
        if (xl.HasValue) sb.Append($"csk-col-xl-{xl.Value} ");
        if (!xs.HasValue && !sm.HasValue && !md.HasValue && !lg.HasValue && !xl.HasValue)
        {
            sb.Append("csk-col-xs-12 ");
        }
        return sb.ToString().TrimEnd();
    }
}
