namespace BlazorFluent.UI.Templates.Wizards;

public interface IWizardHost
{
    void RegisterStep(WizardStep step);
    void UnregisterStep(WizardStep step);
    int CurrentStepIndex { get; }
}
