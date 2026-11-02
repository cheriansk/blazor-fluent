namespace BlazorEnterprise.Shared.UI.Wizards;

public interface IWizardStepComponent<TState>
{
    Task<bool> ValidateStepAsync(WizardContext<TState> context);
    Task OnStepSkippedAsync(WizardContext<TState> context) => Task.CompletedTask;
}
