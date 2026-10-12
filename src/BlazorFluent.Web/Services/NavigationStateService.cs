using BlazorFluent.Core.Contracts;

namespace BlazorFluent.Services;

public class NavigationStateService : INavigationStateService
{
    public bool IsCollapsed { get; private set; }

    public event Action? OnChange;

    public void Toggle()
    {
        IsCollapsed = !IsCollapsed;
        OnChange?.Invoke();
    }

    public void SetCollapsed(bool collapsed)
    {
        if (IsCollapsed != collapsed)
        {
            IsCollapsed = collapsed;
            OnChange?.Invoke();
        }
    }
}
