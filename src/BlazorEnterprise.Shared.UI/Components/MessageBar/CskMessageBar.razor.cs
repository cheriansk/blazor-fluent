using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskMessageBar : ComponentBase
{
    private bool _dismissed;

    [Parameter] public object? Intent { get; set; } = MessageBarIntent.Info;
    [Parameter] public CskAlertSeverity Severity { get; set; } = CskAlertSeverity.Info;
    [Parameter] public CskAlertVariant Variant { get; set; } = CskAlertVariant.Text;
    [Parameter] public string? Title { get; set; }
    [Parameter] public bool AllowDismiss { get; set; }
    [Parameter] public EventCallback OnDismiss { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetSeverityClass()
    {
        var intentStr = Intent?.ToString() ?? Severity.ToString();
        return intentStr.ToLowerInvariant() switch
        {
            "success" => "csk-mb-success",
            "warning" => "csk-mb-warning",
            "error" or "danger" => "csk-mb-error",
            _ => "csk-mb-info"
        };
    }

    private string GetVariantClass() => Variant switch
    {
        CskAlertVariant.Filled => "csk-mb-filled",
        CskAlertVariant.Outlined => "csk-mb-outlined",
        _ => "csk-mb-text"
    };

    private string GetSeverityIcon()
    {
        var intentStr = Intent?.ToString() ?? Severity.ToString();
        return intentStr.ToLowerInvariant() switch
        {
            "success" => CskIcons.CheckmarkCircle,
            "warning" => CskIcons.Warning,
            "error" or "danger" => CskIcons.ErrorCircle,
            _ => CskIcons.Info
        };
    }

    private async Task HandleDismissAsync()
    {
        _dismissed = true;
        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync();
        }
    }
}
