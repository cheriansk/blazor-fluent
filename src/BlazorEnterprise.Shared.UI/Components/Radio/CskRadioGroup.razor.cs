using BlazorEnterprise.Shared.Extensions;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskRadioGroup<TValue> : ComponentBase, ICskRadioGroup
{
    [Parameter] public string GroupName { get; set; } = $"csk-rg-{Guid.NewGuid().ToString("N")[..8]}";
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public Expression<Func<TValue>>? ValueExpression { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    public bool IsDisabled => Disabled;

    public bool IsValueSelected(object? value)
    {
        if (Value == null && value == null) return true;
        if (Value == null || value == null) return false;

        if (value is TValue typedVal)
        {
            return EqualityComparer<TValue>.Default.Equals(Value, typedVal);
        }

        return string.Equals(Value.ToString(), value.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public async Task SelectValueAsync(object? value)
    {
        if (ReadOnly || Disabled) return;

        TValue? typedValue = default;
        if (value is TValue tv)
        {
            typedValue = tv;
        }
        else if (value is not null)
        {
            try
            {
                var targetType = typeof(TValue);
                var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (underlying.IsEnum)
                {
                    if (value.ToString().TryToEnum(underlying, out var enumResult))
                    {
                        typedValue = (TValue?)enumResult;
                    }
                    else
                    {
                        typedValue = default;
                    }
                }
                else
                {
                    typedValue = (TValue)Convert.ChangeType(value, underlying);
                }
            }
            catch
            {
                typedValue = default;
            }
        }

        Value = typedValue;
        if (ValueChanged.HasDelegate && typedValue != null)
        {
            await ValueChanged.InvokeAsync(typedValue);
        }
    }
}
