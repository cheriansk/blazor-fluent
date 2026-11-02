namespace BlazorEnterprise.Shared.UI.Wizards;

public sealed class WizardTemplateRuleBuilder<TState>
{
    private readonly WizardTemplateDefinition<TState> _template;

    public WizardTemplateRuleBuilder(WizardTemplateDefinition<TState> template)
    {
        _template = template;
    }

    public WizardTemplateRuleBuilder<TState> MakeMandatory(string stepId)
    {
        _template.StepOverrides[stepId] = WizardStepRequirement.Mandatory;
        return this;
    }

    public WizardTemplateRuleBuilder<TState> MakeOptional(string stepId)
    {
        _template.StepOverrides[stepId] = WizardStepRequirement.Optional;
        return this;
    }

    public WizardTemplateRuleBuilder<TState> Disable(string stepId)
    {
        _template.StepOverrides[stepId] = WizardStepRequirement.Disabled;
        return this;
    }

    public WizardTemplateRuleBuilder<TState> SetDefaults(Action<TState> applyDefaults)
    {
        _template.ApplyDefaults = applyDefaults;
        return this;
    }
}

public sealed class WizardFlowDefinition<TState>
{
    public List<WizardStepDescriptor<TState>> Steps { get; } = [];
    public Dictionary<string, WizardTemplateDefinition<TState>> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string DefaultTemplateId =>
        Templates.Values.FirstOrDefault(t => t.IsDefault)?.TemplateId
        ?? Templates.Keys.FirstOrDefault()
        ?? "base";

    public List<ActiveStepInfo<TState>> ResolveActiveSteps(string? templateId, TState state)
    {
        var activeTemplate = !string.IsNullOrWhiteSpace(templateId) && Templates.TryGetValue(templateId, out var t)
            ? t
            : Templates.Values.FirstOrDefault(x => x.IsDefault);

        var result = new List<ActiveStepInfo<TState>>();
        var orderedSteps = Steps.OrderBy(s => s.Order).ToList();

        for (int i = 0; i < orderedSteps.Count; i++)
        {
            var step = orderedSteps[i];
            var requirement = step.DefaultRequirement;

            if (activeTemplate != null && activeTemplate.StepOverrides.TryGetValue(step.StepId, out var ovr))
            {
                requirement = ovr;
            }

            if (requirement == WizardStepRequirement.Disabled)
            {
                continue;
            }

            if (step.IsVisiblePredicate != null && !step.IsVisiblePredicate(state))
            {
                continue;
            }

            result.Add(new ActiveStepInfo<TState>
            {
                Descriptor = step,
                Requirement = requirement
            });
        }

        return result;
    }
}

public sealed class ActiveStepInfo<TState>
{
    public required WizardStepDescriptor<TState> Descriptor { get; init; }
    public required WizardStepRequirement Requirement { get; init; }
}

public sealed class WizardFlowBuilder<TState>
{
    private readonly WizardFlowDefinition<TState> _flow = new();

    public WizardFlowBuilder<TState> AddStep<TStepComponent>(
        string stepId,
        string title,
        string? summary = null,
        string? icon = null,
        int order = 100,
        WizardStepRequirement defaultRequirement = WizardStepRequirement.Mandatory,
        Func<TState, bool>? isVisible = null) where TStepComponent : IWizardStepComponent<TState>
    {
        _flow.Steps.RemoveAll(s => string.Equals(s.StepId, stepId, StringComparison.OrdinalIgnoreCase));
        _flow.Steps.Add(new WizardStepDescriptor<TState>
        {
            StepId = stepId,
            Title = title,
            Summary = summary,
            Icon = icon,
            Order = order,
            DefaultRequirement = defaultRequirement,
            ComponentType = typeof(TStepComponent),
            IsVisiblePredicate = isVisible
        });
        return this;
    }

    public WizardFlowBuilder<TState> AddStepAfter<TStepComponent>(
        string targetStepId,
        string newStepId,
        string title,
        string? summary = null,
        string? icon = null,
        WizardStepRequirement defaultRequirement = WizardStepRequirement.Mandatory,
        Func<TState, bool>? isVisible = null) where TStepComponent : IWizardStepComponent<TState>
    {
        var target = _flow.Steps.FirstOrDefault(s => string.Equals(s.StepId, targetStepId, StringComparison.OrdinalIgnoreCase));
        int targetOrder = target?.Order ?? 100;
        
        var nextStep = _flow.Steps
            .Where(s => s.Order > targetOrder)
            .OrderBy(s => s.Order)
            .FirstOrDefault();

        int newOrder = nextStep != null
            ? targetOrder + ((nextStep.Order - targetOrder) / 2)
            : targetOrder + 50;

        if (newOrder == targetOrder)
        {
            newOrder = targetOrder + 1;
        }

        return AddStep<TStepComponent>(newStepId, title, summary, icon, newOrder, defaultRequirement, isVisible);
    }

    public WizardFlowBuilder<TState> AddStepBefore<TStepComponent>(
        string targetStepId,
        string newStepId,
        string title,
        string? summary = null,
        string? icon = null,
        WizardStepRequirement defaultRequirement = WizardStepRequirement.Mandatory,
        Func<TState, bool>? isVisible = null) where TStepComponent : IWizardStepComponent<TState>
    {
        var target = _flow.Steps.FirstOrDefault(s => string.Equals(s.StepId, targetStepId, StringComparison.OrdinalIgnoreCase));
        int targetOrder = target?.Order ?? 100;

        var prevStep = _flow.Steps
            .Where(s => s.Order < targetOrder)
            .OrderByDescending(s => s.Order)
            .FirstOrDefault();

        int newOrder = prevStep != null
            ? prevStep.Order + ((targetOrder - prevStep.Order) / 2)
            : Math.Max(10, targetOrder - 50);

        if (newOrder == targetOrder)
        {
            newOrder = targetOrder - 1;
        }

        return AddStep<TStepComponent>(newStepId, title, summary, icon, newOrder, defaultRequirement, isVisible);
    }

    public WizardFlowBuilder<TState> AddTemplate(
        string templateId,
        string name,
        string? description = null,
        bool isDefault = false,
        Action<WizardTemplateRuleBuilder<TState>>? configure = null)
    {
        var template = new WizardTemplateDefinition<TState>
        {
            TemplateId = templateId,
            Name = name,
            Description = description,
            IsDefault = isDefault
        };

        if (configure != null)
        {
            var ruleBuilder = new WizardTemplateRuleBuilder<TState>(template);
            configure(ruleBuilder);
        }

        _flow.Templates[templateId] = template;
        return this;
    }

    public WizardFlowBuilder<TState> ModifyTemplate(
        string templateId,
        Action<WizardTemplateRuleBuilder<TState>> configure)
    {
        if (_flow.Templates.TryGetValue(templateId, out var template))
        {
            var ruleBuilder = new WizardTemplateRuleBuilder<TState>(template);
            configure(ruleBuilder);
        }
        return this;
    }

    public WizardFlowDefinition<TState> Build() => _flow;
}
