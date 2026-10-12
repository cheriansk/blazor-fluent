namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Scoped tracker for pending, uncommitted in-memory changes across UI tabs and forms.
/// Used to drive the header unsaved changes indicator and warn against accidental data loss.
/// </summary>
public interface IUnsavedChangesStateService
{
    /// <summary>Whether there are any unsaved changes currently pending.</summary>
    bool HasPendingChanges { get; }

    /// <summary>Collection of pending edit descriptions keyed by form/source component identifier.</summary>
    IReadOnlyDictionary<string, string> PendingChanges { get; }

    /// <summary>Registers an active uncommitted edit in a form or tab.</summary>
    void RegisterChange(string sourceKey, string description);

    /// <summary>Clears an uncommitted edit once saved or cancelled.</summary>
    void ClearChange(string sourceKey);

    /// <summary>Clears all pending changes (e.g., on context switch or sign out).</summary>
    void ResetAll();

    /// <summary>Raised whenever the pending changes registry is modified.</summary>
    event Action? OnChange;
}
