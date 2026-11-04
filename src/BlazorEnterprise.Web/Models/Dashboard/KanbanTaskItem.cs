using BlazorEnterprise.Shared.UI.Components;

namespace BlazorEnterprise.Web.Models.Dashboard;

public sealed class KanbanTaskItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Column { get; set; } = "ToDo";
    public string PriorityText { get; set; } = "Medium";
    public CskStatusChip.ChipVariant PriorityVariant { get; set; } = CskStatusChip.ChipVariant.Neutral;
    public string Assignee { get; set; } = string.Empty;
}
