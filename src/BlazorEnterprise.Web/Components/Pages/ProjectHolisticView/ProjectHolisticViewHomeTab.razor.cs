using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.ProjectHolisticViewTabs;

public partial class ProjectHolisticViewHomeTab : ComponentBase
{
    [Parameter]
    public ProjectHealthSummaryDto? HealthSummary { get; set; }

    private static string GetHealthClass(ProjectHealthRatingEnum rating) => rating switch
    {
        ProjectHealthRatingEnum.OnTrack => "health-on-track",
        ProjectHealthRatingEnum.AtRisk => "health-needs-attention",
        ProjectHealthRatingEnum.Critical => "health-critical",
        _ => "health-default"
    };

    private static string GetProgressClass(double pct)
    {
        var clamped = Math.Clamp((int)Math.Round(pct / 5.0) * 5, 0, 100);
        return $"progress-width-{clamped}";
    }
}
