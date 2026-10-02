namespace BlazorFluent.Core.Contracts;

public interface INavigationStateService
{
    bool IsCollapsed { get; }
    event Action? OnChange;
    void Toggle();
    void SetCollapsed(bool collapsed);
}
