using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskTextArea : ComponentBase
{
    private bool _isFocused;

    [Parameter] public string Id { get; set; } = $"csk-textarea-{Guid.NewGuid().ToString("N")[..8]}";
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }
    [Parameter] public int Rows { get; set; } = 3;
    [Parameter] public int? Cols { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? HelperText { get; set; }
    [Parameter] public string? ErrorText { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public int? MaxLength { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public CskTextAreaResize Resize { get; set; } = CskTextAreaResize.Vertical;
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    private async Task HandleInputAsync(ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString();
        Value = newValue;
        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(newValue);
        }
    }

    private async Task HandleChangeAsync(ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString();
        Value = newValue;
        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(newValue);
        }
    }

    private void HandleFocus()
    {
        _isFocused = true;
    }

    private void HandleBlur()
    {
        _isFocused = false;
    }
}
