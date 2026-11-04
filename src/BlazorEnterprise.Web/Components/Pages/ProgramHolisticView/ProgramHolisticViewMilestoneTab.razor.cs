using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.ProgramHolisticViewTabs;

public partial class ProgramHolisticViewMilestoneTab : ComponentBase
{
    [Parameter]
    public IReadOnlyList<CalendarItemDto> Milestones { get; set; } = Array.Empty<CalendarItemDto>();

    [Parameter]
    public IReadOnlyList<CalendarItemDto> AllEvents { get; set; } = Array.Empty<CalendarItemDto>();

    private string? GetParentTimelineTitle(Guid? parentTimelineId)
    {
        if (!parentTimelineId.HasValue || parentTimelineId.Value == Guid.Empty)
            return null;

        var parent = AllEvents.FirstOrDefault(e => e.Id == parentTimelineId.Value);
        return parent?.Title;
    }

    private static string GetStatusChipClass(CalendarItemDto milestone)
    {
        if (milestone.IsOverdue) return "status-overdue";
        return milestone.Status switch
        {
            "Completed" or "Closed" => "status-completed",
            "InProgress" or "Active" => "status-in-progress",
            _ => "status-scheduled"
        };
    }
}
