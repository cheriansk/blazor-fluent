using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskButton : ComponentBase
{
    [Parameter] public CskButtonVariant Variant { get; set; } = CskButtonVariant.Filled;
    [Parameter] public CskButtonSize Size { get; set; } = CskButtonSize.Medium;
    [Parameter] public string? IconStart { get; set; }
    [Parameter] public string? IconEnd { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Loading { get; set; }
    [Parameter] public string Type { get; set; } = "button";
    [Parameter] public string? Href { get; set; }
    [Parameter] public string? Target { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private async Task HandleClickAsync(MouseEventArgs e)
    {
        if (Disabled || Loading) return;
        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(e);
        }
    }

    private string GetVariantClass() => Variant switch
    {
        CskButtonVariant.Outlined => "csk-btn-outlined",
        CskButtonVariant.Text => "csk-btn-text",
        CskButtonVariant.Elevated => "csk-btn-elevated",
        CskButtonVariant.Tonal => "csk-btn-tonal",
        _ => "csk-btn-filled"
    };

    private string GetSizeClass() => Size switch
    {
        CskButtonSize.Small => "csk-btn-sm",
        CskButtonSize.Large => "csk-btn-lg",
        _ => "csk-btn-md"
    };

    private RenderFragment RenderInnerContent() => builder =>
    {
        if (Loading)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "csk-btn-spinner");
            builder.AddAttribute(2, "aria-hidden", "true");
            builder.CloseElement();
        }
        else if (!string.IsNullOrEmpty(IconStart))
        {
            builder.OpenElement(3, "span");
            builder.AddAttribute(4, "class", "csk-btn-icon csk-btn-icon-start");
            builder.OpenComponent<CskIcon>(5);
            builder.AddAttribute(6, "Icon", IconStart);
            builder.CloseComponent();
            builder.CloseElement();
        }

        if (ChildContent is not null)
        {
            builder.OpenElement(7, "span");
            builder.AddAttribute(8, "class", "csk-btn-label");
            builder.AddContent(9, ChildContent);
            builder.CloseElement();
        }

        if (!Loading && !string.IsNullOrEmpty(IconEnd))
        {
            builder.OpenElement(10, "span");
            builder.AddAttribute(11, "class", "csk-btn-icon csk-btn-icon-end");
            builder.OpenComponent<CskIcon>(12);
            builder.AddAttribute(13, "Icon", IconEnd);
            builder.CloseComponent();
            builder.CloseElement();
        }
    };
}
