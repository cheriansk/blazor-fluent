using BlazorEnterprise.Shared.UI.Wizards;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Web.Components.Pages.Settings.ProgramWizard.Steps;

namespace BlazorEnterprise.Web.Components.Pages.Settings.ProgramWizard;

/// <summary>
/// Defines the extensible 4-step Program Creation and Editing flow.
/// </summary>
public static partial class CreateProgramWizardFlow
{
    public static class StepIds
    {
        public const string Details = "program-details";
        public const string Timeline = "program-timeline";
        public const string Milestones = "program-milestones";
        public const string Review = "program-review";
    }

    public static WizardFlowDefinition<CreateProgramWizardState> CreateFlow()
    {
        var builder = new WizardFlowBuilder<CreateProgramWizardState>();

        // Step 1: Program Details & Administrators
        builder.AddStep<ProgramDetailsStep>(
            stepId: StepIds.Details,
            title: "Program Details",
            summary: "Code, Name, Strategic Scope, and Program Administrators",
            order: 100,
            defaultRequirement: WizardStepRequirement.Mandatory);

        // Step 2: Timeline
        builder.AddStep<ProgramTimelineStep>(
            stepId: StepIds.Timeline,
            title: "Timeline",
            summary: "Start Date and Target Completion Date",
            order: 200,
            defaultRequirement: WizardStepRequirement.Mandatory);

        // Step 3: Milestones
        builder.AddStep<ProgramMilestonesStep>(
            stepId: StepIds.Milestones,
            title: "Milestones",
            summary: "Governance checkpoints, release goals, and delivery milestones",
            order: 300,
            defaultRequirement: WizardStepRequirement.Optional);

        // Step 4: Review & Confirm
        builder.AddStep<ProgramReviewStep>(
            stepId: StepIds.Review,
            title: "Review & Confirm",
            summary: "Review and commit program configuration",
            order: 400,
            defaultRequirement: WizardStepRequirement.Mandatory);

        return builder.Build();
    }
}
