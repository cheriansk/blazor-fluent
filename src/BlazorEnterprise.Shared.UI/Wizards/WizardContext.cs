namespace BlazorEnterprise.Shared.UI.Wizards;

/// <summary>
/// Generic in-memory state and lifecycle container for a wizard execution session.
/// Keeps data by-reference across step transitions, preventing DOM/state evaporation in modal dialogs.
/// </summary>
/// <typeparam name="TState">The strongly-typed wizard data model.</typeparam>
public class WizardContext<TState>
{
    public TState State { get; set; }

    public int CurrentStepIndex { get; internal set; } = 0;

    public int TotalSteps { get; internal set; } = 0;

    public bool IsSubmitting { get; internal set; } = false;

    public bool IsDirty { get; internal set; } = false;

    /// <summary>
    /// Key-value dictionary of validation error messages keyed by step index or field name.
    /// </summary>
    public Dictionary<string, string> StepErrors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public event Action? OnStateChanged;

    public WizardContext(TState initialState)
    {
        State = initialState;
    }

    /// <summary>
    /// Flags the wizard state as dirty (user modified inputs), arming the unsaved changes confirmation modal.
    /// </summary>
    public void MarkDirty(bool dirty = true)
    {
        if (IsDirty != dirty)
        {
            IsDirty = dirty;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Signals subscriber components (stepper header, action buttons) that state has updated.
    /// </summary>
    public void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Clears any validation errors recorded for a given step.
    /// </summary>
    public void ClearErrors(string? stepKey = null)
    {
        if (string.IsNullOrEmpty(stepKey))
        {
            StepErrors.Clear();
        }
        else
        {
            StepErrors.Remove(stepKey);
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Sets a validation error for a specific step or field.
    /// </summary>
    public void SetError(string key, string message)
    {
        StepErrors[key] = message;
        NotifyStateChanged();
    }

    /// <summary>
    /// Checks if a given step currently has errors.
    /// </summary>
    public bool HasError(string key) => StepErrors.ContainsKey(key);
}
