using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.ProgramHolisticViewTabs;

public partial class ProgramHolisticViewHomeTab : ComponentBase
{
    [Parameter]
    public ProgramHealthSummaryDto? HealthSummary { get; set; }

    private static string GetHealthClass(ProgramHealthRatingEnum rating) => rating switch
    {
        ProgramHealthRatingEnum.OnTrack => "health-on-track",
        ProgramHealthRatingEnum.AtRisk => "health-needs-attention",
        ProgramHealthRatingEnum.Critical => "health-critical",
        _ => "health-default"
    };

    private static string GetProgressClass(double pct)
    {
        var clamped = Math.Clamp((int)Math.Round(pct / 5.0) * 5, 0, 100);
        return $"progress-width-{clamped}";
    }
}
