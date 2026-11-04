using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.ProjectHolisticViewTabs;

public partial class ProjectHolisticViewMilestoneTab : ComponentBase
{
    [Parameter]
    public IReadOnlyList<CalendarItemDto> Milestones { get; set; } = Array.Empty<CalendarItemDto>();

    [Parameter]
    public IReadOnlyList<CalendarItemDto> AllEvents { get; set; } = Array.Empty<CalendarItemDto>();

    [Parameter]
    public IReadOnlyDictionary<Guid, int>? LinkedWorkItemCounts { get; set; }

    private string? GetParentTimelineTitle(Guid? parentTimelineId)
    {
        if (!parentTimelineId.HasValue || parentTimelineId.Value == Guid.Empty)
            return null;

        var parent = AllEvents.FirstOrDefault(e => e.Id == parentTimelineId.Value);
        return parent?.Title;
    }

    private int GetLinkedWorkItemsCount(Guid milestoneId)
    {
        if (LinkedWorkItemCounts != null && LinkedWorkItemCounts.TryGetValue(milestoneId, out var count))
            return count;
        return 0;
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
