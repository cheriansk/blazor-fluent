using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskLabel : ComponentBase
{
    [Parameter] public object? Typo { get; set; } = CskTypography.Body;
    [Parameter] public object? Color { get; set; } = CskColor.Inherit;
    [Parameter] public CskTextAlign Align { get; set; } = CskTextAlign.Inherit;
    [Parameter] public bool GutterBottom { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetTypoClass()
    {
        var typoStr = Typo?.ToString() ?? "body";
        return typoStr.ToLowerInvariant() switch
        {
            "h1" => "csk-typo-h1",
            "h2" => "csk-typo-h2",
            "h3" => "csk-typo-h3",
            "h4" => "csk-typo-h4",
            "h5" => "csk-typo-h5",
            "h6" or "header" or "pagetitle" => "csk-typo-h6",
            "subtitle" or "subtitle1" or "subtitle2" => "csk-typo-subtitle",
            "subject" => "csk-typo-subject",
            "bodystrong" => "csk-typo-bodystrong",
            "body1" or "body2" or "body" => "csk-typo-body",
            "caption" => "csk-typo-caption",
            "button" => "csk-typo-button",
            "overline" => "csk-typo-overline",
            _ => "csk-typo-body"
        };
    }

    private string GetColorClass()
    {
        var colorStr = Color?.ToString() ?? "inherit";
        return colorStr.ToLowerInvariant() switch
        {
            "primary" => "csk-color-primary",
            "secondary" => "csk-color-secondary",
            "success" => "csk-color-success",
            "warning" => "csk-color-warning",
            "error" or "danger" => "csk-color-error",
            "info" => "csk-color-info",
            "hint" => "csk-color-hint",
            "default" => "csk-color-default",
            _ => "csk-color-inherit"
        };
    }

    private string GetAlignClass() => Align switch
    {
        CskTextAlign.Left => "csk-align-left",
        CskTextAlign.Center => "csk-align-center",
        CskTextAlign.Right => "csk-align-right",
        CskTextAlign.Justify => "csk-align-justify",
        _ => "csk-align-inherit"
    };
}
