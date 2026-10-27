using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskDialogBody : ComponentBase
{
    [Parameter] public RenderFragment? TitleTemplate { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? ActionTemplate { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
}
