using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskDatePicker : ComponentBase
{
    [Parameter] public string Id { get; set; } = $"csk-date-{Guid.NewGuid():N}";
    [Parameter] public string? Label { get; set; }
    [Parameter] public DateTime? Value { get; set; }
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }
    [Parameter] public DateTime? Min { get; set; }
    [Parameter] public DateTime? Max { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public string? HelperText { get; set; }
    [Parameter] public string? ErrorText { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool HasError => !string.IsNullOrEmpty(ErrorText);

    private string? FormattedValue => Value?.ToString("yyyy-MM-dd");
    private string? FormattedMin => Min?.ToString("yyyy-MM-dd");
    private string? FormattedMax => Max?.ToString("yyyy-MM-dd");

    private async Task HandleChangeAsync(ChangeEventArgs e)
    {
        var stringVal = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(stringVal))
        {
            Value = null;
        }
        else if (DateTime.TryParse(stringVal, out var parsed))
        {
            Value = parsed;
        }

        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(Value);
        }
    }
}
