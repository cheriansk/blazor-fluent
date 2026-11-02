namespace BlazorEnterprise.Shared.UI.Wizards;

public sealed record WizardStepDescriptor<TState>
{
    public required string StepId { get; init; }
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public string? Icon { get; init; }
    public required Type ComponentType { get; init; }
    public int Order { get; init; } = 100;
    public WizardStepRequirement DefaultRequirement { get; init; } = WizardStepRequirement.Mandatory;
    public Func<TState, bool>? IsVisiblePredicate { get; init; }
}

public sealed class WizardTemplateDefinition<TState>
{
    public required string TemplateId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public Dictionary<string, WizardStepRequirement> StepOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Action<TState>? ApplyDefaults { get; set; }
}
