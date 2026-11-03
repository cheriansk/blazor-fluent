using BlazorEnterprise.Shared.UI.Components;

namespace BlazorEnterprise.Web.Models.Dashboard;

public sealed class AuditActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
    public string BadgeText { get; set; } = "Info";
    public CskStatusChip.ChipVariant BadgeVariant { get; set; } = CskStatusChip.ChipVariant.Info;
    public string Icon { get; set; } = CskIcons.Info;
}
