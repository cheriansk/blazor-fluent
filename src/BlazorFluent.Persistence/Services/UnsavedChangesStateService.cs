using BlazorFluent.Core.Contracts;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Scoped implementation of <see cref="IUnsavedChangesStateService"/>.
/// </summary>
public sealed class UnsavedChangesStateService : IUnsavedChangesStateService
{
    private readonly Dictionary<string, string> _pending = new(StringComparer.OrdinalIgnoreCase);

    public event Action? OnChange;

    public bool HasPendingChanges => _pending.Count > 0;

    public IReadOnlyDictionary<string, string> PendingChanges => _pending;

    public void RegisterChange(string sourceKey, string description)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)) return;

        _pending[sourceKey] = description;
        OnChange?.Invoke();
    }

    public void ClearChange(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)) return;

        if (_pending.Remove(sourceKey))
        {
            OnChange?.Invoke();
        }
    }

    public void ResetAll()
    {
        if (_pending.Count > 0)
        {
            _pending.Clear();
            OnChange?.Invoke();
        }
    }
}
