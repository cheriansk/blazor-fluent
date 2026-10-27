using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskBadge : ComponentBase
{
    [Parameter] public object? Variant { get; set; } = CskBadgeVariant.Filled;
    [Parameter] public object? Appearance { get; set; }
    [Parameter] public object? Color { get; set; }
    [Parameter] public object? Size { get; set; }
    [Parameter] public string? BackgroundColor { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetVariantClass()
    {
        var variantStr = Appearance?.ToString() ?? Variant?.ToString() ?? "filled";
        return variantStr.ToLowerInvariant() switch
        {
            "tint" or "subtle" or "lightweight" or "ghost" => "csk-badge-tint",
            "outline" or "outlined" => "csk-badge-outline",
            "accent" => "csk-badge-filled",
            _ => "csk-badge-filled"
        };
    }

    private string GetColorClass()
    {
        var colorStr = Color?.ToString() ?? "default";
        return colorStr.ToLowerInvariant() switch
        {
            "primary" or "brand" or "accent" => "csk-badge-primary",
            "secondary" => "csk-badge-secondary",
            "success" => "csk-badge-success",
            "warning" or "severe" => "csk-badge-warning",
            "error" or "danger" or "important" => "csk-badge-error",
            "informative" or "info" => "csk-badge-info",
            "subtle" or "neutral" => "csk-badge-subtle",
            _ => "csk-badge-default"
        };
    }

    private string GetSizeClass()
    {
        var sizeStr = Size?.ToString() ?? "small";
        return sizeStr.ToLowerInvariant() switch
        {
            "large" or "size32" => "csk-badge-lg",
            "medium" or "size24" => "csk-badge-md",
            _ => "csk-badge-sm"
        };
    }
}
