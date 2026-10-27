using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Wizards;

public partial class CskWizardModal<TState> : ComponentBase, IWizardHost, IDisposable
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public string Title { get; set; } = "Wizard";
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public string? FinishButtonText { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public TState InitialState { get; set; } = default!;
    [Parameter] public EventCallback<TState> OnFinish { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public RenderFragment? Steps { get; set; }

    public WizardContext<TState> Context { get; private set; } = default!;

    private readonly List<WizardStep> _steps = new();
    private int _currentStepIndex = 0;
    private bool _isBusy = false;
    private bool _showDiscardConfirm = false;
    private string? _activeErrorMessage;

    public int CurrentStepIndex => _currentStepIndex;

    protected override void OnInitialized()
    {
        Context = new WizardContext<TState>(InitialState);
        Context.OnStateChanged += HandleContextStateChanged;
    }

    protected override void OnParametersSet()
    {
        if (Context != null && !EqualityComparer<TState>.Default.Equals(InitialState, default) && !Context.IsDirty)
        {
            Context.State = InitialState;
        }
    }

    public void RegisterStep(WizardStep step)
    {
        if (!_steps.Contains(step))
        {
            step.StepIndex = _steps.Count;
            _steps.Add(step);
            Context.TotalSteps = _steps.Count;
            StateHasChanged();
        }
    }

    public void UnregisterStep(WizardStep step)
    {
        if (_steps.Remove(step))
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].StepIndex = i;
            }
            Context.TotalSteps = _steps.Count;
            StateHasChanged();
        }
    }

    private void HandleContextStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task HandleBackdropClick()
    {
        await RequestCloseAsync();
    }

    private async Task RequestCloseAsync()
    {
        if (Context.IsDirty)
        {
            _showDiscardConfirm = true;
        }
        else
        {
            await CloseModalAsync();
        }
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

    private async Task GoNextAsync()
    {
/*        if (_isBusy || _currentStepIndex >= _steps.Count - 1) return;

        _activeErrorMessage = null;
        var currentStep = _steps[_currentStepIndex];

        if (currentStep.OnValidateAsync != null)
        {
            _isBusy = true;
            StateHasChanged();

            try
            {
                var isValid = await currentStep.OnValidateAsync(Context);
                if (!isValid)
                {
                    _isBusy = false;
                    StateHasChanged();
                    return;
                }
            }
            catch (Exception ex)
            {
                _activeErrorMessage = ex.Message;
                _isBusy = false;
                StateHasChanged();
                return;
            }
            finally
            {
                _isBusy = false;
            }
        }

        _currentStepIndex++;
        Context.CurrentStepIndex = _currentStepIndex;
        Context.NotifyStateChanged();
        StateHasChanged();*/
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
    {/*
        if (_isBusy) return;

        _activeErrorMessage = null;
        var currentStep = _steps[_currentStepIndex];

        if (currentStep.OnValidateAsync != null)
        {
            _isBusy = true;
            StateHasChanged();

            try
            {
                var isValid = await currentStep.OnValidateAsync(Context);
                if (!isValid)
                {
                    _isBusy = false;
                    StateHasChanged();
                    return;
                }
            }
            catch (Exception ex)
            {
                _activeErrorMessage = ex.Message;
                _isBusy = false;
                StateHasChanged();
                return;
            }
            finally
            {
                _isBusy = false;
            }
        }

        _isBusy = true;
        StateHasChanged();

        try
        {
            if (OnFinish.HasDelegate)
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
        }*/
    }

    public void Dispose()
    {
        if (Context != null)
        {
            Context.OnStateChanged -= HandleContextStateChanged;
        }
    }
}
