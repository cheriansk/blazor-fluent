using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskTextInput : ComponentBase
{
    private bool _isFocused;

    [Parameter] public string Id { get; set; } = $"csk-input-{Guid.NewGuid().ToString("N")[..8]}";
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }
    [Parameter] public string Type { get; set; } = "text";
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? HelperText { get; set; }
    [Parameter] public string? ErrorText { get; set; }
    [Parameter] public string? IconStart { get; set; }
    [Parameter] public string? IconEnd { get; set; }
    [Parameter] public RenderFragment? StartTemplate { get; set; }
    [Parameter] public RenderFragment? EndTemplate { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
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
