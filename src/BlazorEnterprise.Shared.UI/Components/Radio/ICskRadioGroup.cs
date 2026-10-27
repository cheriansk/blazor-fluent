namespace BlazorEnterprise.Shared.UI.Components;

public interface ICskRadioGroup
{
    string GroupName { get; }
    bool IsDisabled { get; }
    bool IsValueSelected(object? value);
    Task SelectValueAsync(object? value);
}
