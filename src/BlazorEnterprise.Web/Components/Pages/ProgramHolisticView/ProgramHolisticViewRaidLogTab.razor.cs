using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.ProgramHolisticViewTabs;

public partial class ProgramHolisticViewRaidLogTab : ComponentBase
{
    public enum RaidSection
    {
        Risks,
        Assumptions,
        Issues,
        Decisions
    }

    [Parameter]
    public IReadOnlyList<RiskEntity> Risks { get; set; } = Array.Empty<RiskEntity>();

    [Parameter]
    public IReadOnlyList<AssumptionEntity> Assumptions { get; set; } = Array.Empty<AssumptionEntity>();

    [Parameter]
    public IReadOnlyList<IssueEntity> Issues { get; set; } = Array.Empty<IssueEntity>();

    [Parameter]
    public IReadOnlyList<WorkDependencyEntity> Blockers { get; set; } = Array.Empty<WorkDependencyEntity>();

    private RaidSection _selectedSection = RaidSection.Risks;

    private void SetSection(RaidSection section)
    {
        _selectedSection = section;
    }

    private static string GetExposureBadgeClass(int score) => score switch
    {
        >= 9 => "exposure-critical",
        >= 6 => "exposure-high",
        >= 3 => "exposure-medium",
        _ => "exposure-low"
    };

    private static string GetSeverityClass(IssueSeverityEnum severity) => severity switch
    {
        IssueSeverityEnum.Critical => "severity-critical",
        IssueSeverityEnum.High => "severity-high",
        IssueSeverityEnum.Medium => "severity-medium",
        _ => "severity-low"
    };
}
