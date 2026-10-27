using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskLink : ComponentBase
{
    [Parameter] public string? Href { get; set; }
    [Parameter] public string? Target { get; set; }
    [Parameter] public object? Appearance { get; set; } = CskLinkAppearance.Normal;
    [Parameter] public CskUnderline Underline { get; set; } = CskUnderline.Hover;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetAppearanceClass()
    {
        var appStr = Appearance?.ToString() ?? "normal";
        return appStr.ToLowerInvariant() switch
        {
            "subtle" => "csk-link-subtle",
            _ => "csk-link-normal"
        };
    }

    private string GetUnderlineClass() => Underline switch
    {
        CskUnderline.Always => "csk-link-underline-always",
        CskUnderline.None => "csk-link-underline-none",
        _ => "csk-link-underline-hover"
    };

    private async Task HandleClickAsync(MouseEventArgs e)
    {
        if (Disabled) return;
        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(e);
        }
    }
}
