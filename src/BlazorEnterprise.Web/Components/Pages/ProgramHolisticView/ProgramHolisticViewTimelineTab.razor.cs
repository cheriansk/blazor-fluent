using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.ProgramHolisticViewTabs;

public partial class ProgramHolisticViewTimelineTab : ComponentBase
{
    [Inject]
    public ICalendarService CalendarService { get; set; } = default!;

    [Parameter]
    public Guid ProgramId { get; set; }

    [Parameter]
    public Guid TenantId { get; set; }

    [Parameter]
    public IReadOnlyList<CalendarItemDto> Events { get; set; } = Array.Empty<CalendarItemDto>();

    [Parameter]
    public EventCallback OnDataChanged { get; set; }

    private IReadOnlyList<CalendarItemDto> TimelineRows =>
        Events.Where(e => string.Equals(e.ItemCategory, nameof(CalendarEntryTypeEnum.Timeline), StringComparison.OrdinalIgnoreCase))
              .OrderBy(e => e.StartUtc)
              .ToList();

    private IReadOnlyList<CalendarItemDto> AllMilestones =>
        Events.Where(e => string.Equals(e.ItemCategory, nameof(CalendarEntryTypeEnum.Milestone), StringComparison.OrdinalIgnoreCase))
              .OrderBy(e => e.StartUtc)
              .ToList();

    private int TotalMilestonesCount => AllMilestones.Count;

    private List<CalendarItemDto> GetMilestonesForTimeline(Guid timelineId) =>
        AllMilestones.Where(m => m.ParentTimelineId == timelineId).ToList();
}
