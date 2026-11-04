using System.Text.Json;

namespace BlazorEnterprise.Shared.UI.Wizards;

/// <summary>
/// Generic in-memory state and lifecycle container for a wizard execution session.
/// Keeps data by-reference across step transitions, preventing DOM/state evaporation in modal dialogs.
/// </summary>
/// <typeparam name="TState">The strongly-typed wizard data model.</typeparam>
public class WizardContext<TState>
{
    private string? _initialStateJson;

    public TState State { get; set; }

    public int CurrentStepIndex { get; internal set; } = 0;

    public int TotalSteps { get; internal set; } = 0;

    public bool IsSubmitting { get; internal set; } = false;

    public bool IsReadOnly { get; set; } = false;

    public bool IsDirty { get; internal set; } = false;

    public string? SelectedTemplateId { get; private set; }

    public HashSet<string> SkippedStepIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Key-value dictionary of validation error messages keyed by step index or field name.
    /// </summary>
    public Dictionary<string, string> StepErrors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public event Action? OnStateChanged;
    public event Action<string?>? OnTemplateChanged;

    public WizardContext(TState initialState, string? initialTemplateId = null)
    {
        State = initialState;
        SelectedTemplateId = initialTemplateId;
        CaptureInitialSnapshot();
    }

    /// <summary>
    /// Captures a deep serialized snapshot of the state to accurately detect modifications and render diffs.
    /// </summary>
    public void CaptureInitialSnapshot()
    {
        try
        {
            _initialStateJson = State != null ? JsonSerializer.Serialize(State) : null;
        }
        catch
        {
            _initialStateJson = null;
        }
    }

    /// <summary>
    /// Returns the deep baseline snapshot of the initial state.
    /// </summary>
    public TState? GetInitialStateSnapshot()
    {
        if (string.IsNullOrEmpty(_initialStateJson)) return default;
        try
        {
            return JsonSerializer.Deserialize<TState>(_initialStateJson);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Checks whether the serialized state differs from the initial snapshot.
    /// </summary>
    public bool HasStateChanged()
    {
        if (IsDirty) return true;
        if (string.IsNullOrEmpty(_initialStateJson)) return false;
        try
        {
            var currentJson = State != null ? JsonSerializer.Serialize(State) : null;
            return !string.Equals(_initialStateJson, currentJson, StringComparison.Ordinal);
        }
        catch
        {
            return IsDirty;
        }
    }

    public void SetTemplate(string? templateId)
    {
        if (!string.Equals(SelectedTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
        {
            SelectedTemplateId = templateId;
            OnTemplateChanged?.Invoke(templateId);
            NotifyStateChanged();
        }
    }

    public void MarkStepSkipped(string stepId, bool skipped = true)
    {
        if (skipped)
        {
            SkippedStepIds.Add(stepId);
        }
        else
        {
            SkippedStepIds.Remove(stepId);
        }
        NotifyStateChanged();
    }

    public bool IsStepSkipped(string stepId) => SkippedStepIds.Contains(stepId);

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
