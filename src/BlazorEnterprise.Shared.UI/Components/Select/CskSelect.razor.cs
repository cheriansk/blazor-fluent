using BlazorEnterprise.Shared.Extensions;
using System.ComponentModel;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskSelect<TOption, TValue> : ComponentBase, ICskSelect
{
    private bool _isFocused;

    [Parameter] public string Id { get; set; } = $"csk-select-{Guid.NewGuid().ToString("N")[..8]}";
    [Parameter] public string? Label { get; set; }
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public Expression<Func<TValue>>? ValueExpression { get; set; }
    [Parameter] public IEnumerable<TOption>? Items { get; set; }
    [Parameter] public Func<TOption, string>? OptionText { get; set; }
    [Parameter] public Func<TOption, object?>? OptionValue { get; set; }
    [Parameter] public Func<TOption, bool>? OptionDisabled { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public string? HelperText { get; set; }
    [Parameter] public string? ErrorText { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public string CurrentStringValue => Value?.ToString() ?? string.Empty;
    public bool IsPlaceholderSelected => Value == null || string.IsNullOrEmpty(CurrentStringValue);
    public bool IsPlaceholderDisabled => Required;

    public bool IsValueSelected(string? optionValue)
    {
        return string.Equals(CurrentStringValue, optionValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsItemValueSelected(string? itemValue)
    {
        return IsValueSelected(itemValue);
    }

    private async Task HandleChangeAsync(ChangeEventArgs e)
    {
        var rawString = e.Value?.ToString();
        var converted = ConvertFromString(rawString);
        Value = converted;
        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(converted);
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

    private static TValue? ConvertFromString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        var targetType = typeof(TValue);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
        {
            return (TValue?)(object)value;
        }

        if (underlyingType.IsEnum)
        {
            if (value.TryToEnum(underlyingType, out var enumResult))
            {
                return (TValue?)enumResult;
            }
            return default;
        }

        if (underlyingType == typeof(Guid))
        {
            if (Guid.TryParse(value, out var guidResult))
            {
                return (TValue?)(object)guidResult;
            }
            return default;
        }

        if (underlyingType == typeof(int))
        {
            if (int.TryParse(value, out var intResult))
            {
                return (TValue?)(object)intResult;
            }
            return default;
        }

        if (underlyingType == typeof(long))
        {
            if (long.TryParse(value, out var longResult))
            {
                return (TValue?)(object)longResult;
            }
            return default;
        }

        if (underlyingType == typeof(bool))
        {
            if (bool.TryParse(value, out var boolResult))
            {
                return (TValue?)(object)boolResult;
            }
            return default;
        }

        try
        {
            var converter = TypeDescriptor.GetConverter(underlyingType);
            if (converter.CanConvertFrom(typeof(string)))
            {
                return (TValue?)converter.ConvertFromString(value);
            }
            return (TValue?)Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return default;
        }
    }
}
