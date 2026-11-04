using BlazorEnterprise.Shared.UI.Wizards;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Web.Components.Pages.Settings.ProjectWizard.Steps;

namespace BlazorEnterprise.Web.Components.Pages.Settings.ProjectWizard;

public static partial class CreateProjectWizardFlow
{
    public static class StepIds
    {
        public const string BasicDetails      = "basic-details";
        public const string ProjectTimeline   = "project-timeline";
        public const string MilestoneCalendar = "milestone-calendar";
        public const string TeamRoster        = "team-roster";
        public const string ProjectReview     = "project-review";
    }

    public static WizardFlowDefinition<CreateProjectWizardState> CreateFlow()
    {
        var builder = new WizardFlowBuilder<CreateProjectWizardState>();

        // 1. Register Standard Base Steps with order spacing
        builder
            .AddStep<ProjectBasicDetailsStep>(
                stepId: StepIds.BasicDetails,
                title: "Basic Details",
                summary: "Name, code & template",
                order: 100,
                defaultRequirement: WizardStepRequirement.Mandatory)
            .AddStep<ProjectTimelineStep>(
                stepId: StepIds.ProjectTimeline,
                title: "Project Timeline",
                summary: "Execution start & target dates",
                order: 200,
                defaultRequirement: WizardStepRequirement.Mandatory)
            .AddStep<ProjectMilestoneCalendarStep>(
                stepId: StepIds.MilestoneCalendar,
                title: "Milestone Calendar",
                summary: "Deliverables & stage gates",
                order: 300,
                defaultRequirement: WizardStepRequirement.Mandatory)
            .AddStep<ProjectTeamRosterStep>(
                stepId: StepIds.TeamRoster,
                title: "Team Roster",
                summary: "Assign governance leads & team",
                order: 400,
                defaultRequirement: WizardStepRequirement.Mandatory)
            .AddStep<ProjectReviewStep>(
                stepId: StepIds.ProjectReview,
                title: "Review & Confirm",
                summary: "Review project details & finalize",
                order: 500,
                defaultRequirement: WizardStepRequirement.Mandatory);

        // 2. Base Template: All 4 steps are Mandatory
        builder.AddTemplate(
            templateId: "base",
            name: "Base Template",
            description: "Standard 4-step project governance flow.",
            isDefault: true);

        // 3. Internal Fast-Track Template: Milestone Calendar is optional
        builder.AddTemplate(
            templateId: "internal-fast-track",
            name: "Internal Fast-Track",
            description: "Fast-track execution; Milestone Calendar is optional.",
            configure: rules => rules
                .MakeOptional(StepIds.MilestoneCalendar));

        // 4. Enterprise Template: All steps mandatory with milestones review
        builder.AddTemplate(
            templateId: "enterprise",
            name: "Enterprise Delivery",
            description: "Standard enterprise execution with mandatory milestones.",
            configure: rules => rules
                .MakeMandatory(StepIds.MilestoneCalendar));

        // 5. Downstream Extension Hook for Cloning Teams (.Custom.cs)
        ConfigureCustom(builder);

        return builder.Build();
    }

    static partial void ConfigureCustom(WizardFlowBuilder<CreateProjectWizardState> flow);
}
