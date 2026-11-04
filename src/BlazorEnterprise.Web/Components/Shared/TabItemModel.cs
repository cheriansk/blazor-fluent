using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Shared;

/// <summary>
/// Model representing a single tab item in <see cref="TabContainerComp"/>.
/// Fully decoupled from third-party UI libraries, leveraging native CskTabs.
/// </summary>
public class TabItemModel
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public object? Badge { get; set; }
    public string? BadgeClass { get; set; }
    public RenderFragment? Content { get; set; }
}
