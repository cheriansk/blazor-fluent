using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Wizards;

public partial class CskWizardModal<TState> : ComponentBase, IWizardHost, IDisposable
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public bool IsReadOnly { get; set; }
    [Parameter] public string Title { get; set; } = "Wizard";
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public string? FinishButtonText { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public TState InitialState { get; set; } = default!;
    [Parameter] public string? InitialTemplateId { get; set; }
    [Parameter] public EventCallback<TState> OnFinish { get; set; }
    [Parameter] public Func<TState, Task<string?>>? OnSaveAsync { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public EventCallback OnEditRequested { get; set; }
    [Parameter] public RenderFragment? Steps { get; set; }
    [Parameter] public WizardFlowDefinition<TState>? FlowDefinition { get; set; }

    public WizardContext<TState> Context { get; private set; } = default!;

    private readonly List<WizardStep> _declarativeSteps = new();
    private List<ActiveStepInfo<TState>> _resolvedFlowSteps = new();
    private DynamicComponent? _dynamicStepComponentRef;
    private int _currentStepIndex = 0;
    private bool _isBusy = false;
    private bool _showDiscardConfirm = false;
    private bool _wasOpen = false;
    private bool _wasReadOnly = false;
    private string? _activeErrorMessage;

    public int CurrentStepIndex => _currentStepIndex;

    private int ActiveStepCount => FlowDefinition != null ? _resolvedFlowSteps.Count : _declarativeSteps.Count;

    private ActiveStepInfo<TState>? CurrentActiveStep =>
        FlowDefinition != null && _currentStepIndex >= 0 && _currentStepIndex < _resolvedFlowSteps.Count
            ? _resolvedFlowSteps[_currentStepIndex]
            : null;

    private bool IsCurrentStepOptional =>
        CurrentActiveStep?.Requirement == WizardStepRequirement.Optional;

    protected override void OnInitialized()
    {
        if (IsOpen)
        {
            InitializeContext();
            _wasOpen = true;
            _wasReadOnly = IsReadOnly;
        }
        RecomputeFlowSteps();
    }

    protected override void OnParametersSet()
    {
        if (IsOpen && (!_wasOpen || Context == null))
        {
            InitializeContext();
            _currentStepIndex = 0;
            _activeErrorMessage = null;
        }
        else if (Context != null)
        {
            var switchedFromReadOnlyToEdit = _wasReadOnly && !IsReadOnly;
            Context.IsReadOnly = IsReadOnly;

            // Only synchronize initial state if transitioning from ReadOnly to Edit mode
            if (switchedFromReadOnlyToEdit && !EqualityComparer<TState>.Default.Equals(InitialState, default))
            {
                Context.State = InitialState;
                Context.CaptureInitialSnapshot();
                Context.MarkDirty(false);
                Context.NotifyStateChanged();
            }
        }
        _wasOpen = IsOpen;
        _wasReadOnly = IsReadOnly;

        RecomputeFlowSteps();
    }

    private void InitializeContext()
    {
        if (Context != null)
        {
            Context.OnStateChanged -= HandleContextStateChanged;
            Context.OnTemplateChanged -= HandleTemplateChanged;
        }

        var templateId = InitialTemplateId ?? FlowDefinition?.DefaultTemplateId;
        Context = new WizardContext<TState>(InitialState, templateId)
        {
            IsReadOnly = IsReadOnly
        };
        Context.OnStateChanged += HandleContextStateChanged;
        Context.OnTemplateChanged += HandleTemplateChanged;
    }

    private void HandleTemplateChanged(string? newTemplateId)
    {
        RecomputeFlowSteps();
        if (_currentStepIndex >= ActiveStepCount)
        {
            _currentStepIndex = Math.Max(0, ActiveStepCount - 1);
            Context.CurrentStepIndex = _currentStepIndex;
        }
        InvokeAsync(StateHasChanged);
    }

    private void RecomputeFlowSteps()
    {
        if (FlowDefinition != null && Context != null)
        {
            _resolvedFlowSteps = FlowDefinition.ResolveActiveSteps(Context.SelectedTemplateId, Context.State);
            Context.TotalSteps = _resolvedFlowSteps.Count;
        }
    }

    public void RegisterStep(WizardStep step)
    {
        if (!_declarativeSteps.Contains(step))
        {
            step.StepIndex = _declarativeSteps.Count;
            _declarativeSteps.Add(step);
            Context.TotalSteps = _declarativeSteps.Count;
            StateHasChanged();
        }
    }

    public void UnregisterStep(WizardStep step)
    {
        if (_declarativeSteps.Remove(step))
        {
            for (int i = 0; i < _declarativeSteps.Count; i++)
            {
                _declarativeSteps[i].StepIndex = i;
            }
            Context.TotalSteps = _declarativeSteps.Count;
            StateHasChanged();
        }
    }

    private string GetStepTitle(int index)
    {
        if (FlowDefinition != null && index >= 0 && index < _resolvedFlowSteps.Count)
        {
            return _resolvedFlowSteps[index].Descriptor.Title;
        }
        if (index >= 0 && index < _declarativeSteps.Count)
        {
            return _declarativeSteps[index].Title;
        }
        return $"Step {index + 1}";
    }

    private string? GetStepSummary(int index)
    {
        if (FlowDefinition != null && index >= 0 && index < _resolvedFlowSteps.Count)
        {
            return _resolvedFlowSteps[index].Descriptor.Summary;
        }
        if (index >= 0 && index < _declarativeSteps.Count)
        {
            return _declarativeSteps[index].Summary;
        }
        return null;
    }

    private bool IsStepOptional(int index)
    {
        if (FlowDefinition != null && index >= 0 && index < _resolvedFlowSteps.Count)
        {
            return _resolvedFlowSteps[index].Requirement == WizardStepRequirement.Optional;
        }
        return false;
    }

    private bool IsStepSkipped(int index)
    {
        if (FlowDefinition != null && index >= 0 && index < _resolvedFlowSteps.Count)
        {
            return Context.IsStepSkipped(_resolvedFlowSteps[index].Descriptor.StepId);
        }
        return false;
    }

    private Dictionary<string, object> GetStepParameters()
    {
        var parameters = new Dictionary<string, object>();
        if (Context != null && CurrentActiveStep != null)
        {
            var compType = CurrentActiveStep.Descriptor.ComponentType;
            var prop = compType.GetProperty("Context");
            if (prop != null && prop.CanWrite && prop.PropertyType.IsAssignableFrom(Context.GetType()))
            {
                parameters["Context"] = Context;
            }
        }
        return parameters;
    }

    private void HandleContextStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task HandleBackdropClick()
    {
        await RequestCloseAsync();
    }

    private async Task HandleEditRequestedAsync()
    {
        if (OnEditRequested.HasDelegate)
        {
            await OnEditRequested.InvokeAsync();
        }
    }

    private async Task RequestCloseAsync()
    {
        if (HasUnsavedChanges())
        {
            _showDiscardConfirm = true;
        }
        else
        {
            await CloseModalAsync();
        }
    }

    private bool HasUnsavedChanges()
    {
        if (IsReadOnly || Context == null) return false;
        if (Context.IsDirty || Context.HasStateChanged()) return true;

        if (_dynamicStepComponentRef?.Instance is IWizardStepComponent<TState> stepComp && stepComp.HasPendingChanges())
        {
            return true;
        }

        return false;
    }

    private async Task ConfirmDiscardAsync()
    {
        _showDiscardConfirm = false;
        await CloseModalAsync();
    }

    private async Task CloseModalAsync()
    {
        IsOpen = false;
        if (IsOpenChanged.HasDelegate)
        {
            await IsOpenChanged.InvokeAsync(false);
        }
        if (OnCancel.HasDelegate)
        {
            await OnCancel.InvokeAsync();
        }
    }

    private async Task<bool> ValidateCurrentStepAsync()
    {
        _activeErrorMessage = null;

        if (FlowDefinition != null && CurrentActiveStep != null)
        {
            if (_dynamicStepComponentRef?.Instance is IWizardStepComponent<TState> stepComp)
            {
                try
                {
                    var isValid = await stepComp.ValidateStepAsync(Context);
                    if (!isValid)
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _activeErrorMessage = ex.Message;
                    return false;
                }
            }
            Context.MarkStepSkipped(CurrentActiveStep.Descriptor.StepId, false);
            return true;
        }

        if (_currentStepIndex >= 0 && _currentStepIndex < _declarativeSteps.Count)
        {
            var step = _declarativeSteps[_currentStepIndex];
            try
            {
                return await step.ValidateAsync();
            }
            catch (Exception ex)
            {
                _activeErrorMessage = ex.Message;
                return false;
            }
        }

        return true;
    }

    private async Task GoNextAsync()
    {
        if (_isBusy || _currentStepIndex >= ActiveStepCount - 1) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var isValid = await ValidateCurrentStepAsync();
            if (!isValid)
            {
                return;
            }

            _currentStepIndex++;
            Context.CurrentStepIndex = _currentStepIndex;
            Context.NotifyStateChanged();
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task GoSkipAsync()
    {
        if (_isBusy || !IsCurrentStepOptional || _currentStepIndex >= ActiveStepCount - 1) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            if (CurrentActiveStep != null)
            {
                Context.MarkStepSkipped(CurrentActiveStep.Descriptor.StepId, true);
                if (_dynamicStepComponentRef?.Instance is IWizardStepComponent<TState> stepComp)
                {
                    await stepComp.OnStepSkippedAsync(Context);
                }
            }

            _currentStepIndex++;
            Context.CurrentStepIndex = _currentStepIndex;
            Context.NotifyStateChanged();
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private Task GoPreviousAsync()
    {
        if (_isBusy || _currentStepIndex <= 0) return Task.CompletedTask;

        _activeErrorMessage = null;
        _currentStepIndex--;
        Context.CurrentStepIndex = _currentStepIndex;
        Context.NotifyStateChanged();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleFinishAsync()
    {
        if (_isBusy) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var isValid = await ValidateCurrentStepAsync();
            if (!isValid)
            {
                return;
            }

            if (OnSaveAsync != null)
            {
                var error = await OnSaveAsync(Context.State);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _activeErrorMessage = error;
                    return;
                }
            }
            else if (OnFinish.HasDelegate)
            {
                await OnFinish.InvokeAsync(Context.State);
            }

            await CloseModalAsync();
        }
        catch (Exception ex)
        {
            _activeErrorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        if (Context != null)
        {
            Context.OnStateChanged -= HandleContextStateChanged;
            Context.OnTemplateChanged -= HandleTemplateChanged;
        }
    }
}
