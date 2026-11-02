namespace BlazorEnterprise.Shared.UI.Wizards;

public enum WizardStepRequirement
{
    Mandatory = 0,
    Optional  = 1,
    Disabled  = 2
}

public enum WizardStepStatus
{
    Pending   = 0,
    Active    = 1,
    Completed = 2,
    Skipped   = 3
}
