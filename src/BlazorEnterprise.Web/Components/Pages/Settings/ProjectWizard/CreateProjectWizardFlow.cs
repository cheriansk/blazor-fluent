using BlazorEnterprise.Shared.UI.Wizards;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Web.Components.Pages.Settings.ProjectWizard.Steps;

namespace BlazorEnterprise.Web.Components.Pages.Settings.ProjectWizard;

public static partial class CreateProjectWizardFlow
{
    public static class StepIds
    {
        public const string BasicDetails      = "basic-details";
        public const string RfpTimeline       = "rfp-timeline";
        public const string ProjectTimeline   = "project-timeline";
        public const string MilestoneCalendar = "milestone-calendar";
        public const string TeamRoster        = "team-roster";
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
            .AddStep<ProjectRfpTimelineStep>(
                stepId: StepIds.RfpTimeline,
                title: "RFP Timeline",
                summary: "Solicitation milestones & dates",
                order: 200,
                defaultRequirement: WizardStepRequirement.Mandatory)
            .AddStep<ProjectTimelineStep>(
                stepId: StepIds.ProjectTimeline,
                title: "Project Timeline",
                summary: "Execution start & target dates",
                order: 300,
                defaultRequirement: WizardStepRequirement.Mandatory)
            .AddStep<ProjectMilestoneCalendarStep>(
                stepId: StepIds.MilestoneCalendar,
                title: "Milestone Calendar",
                summary: "Deliverables & stage gates",
                order: 400,
                defaultRequirement: WizardStepRequirement.Mandatory)
            .AddStep<ProjectTeamRosterStep>(
                stepId: StepIds.TeamRoster,
                title: "Team Roster",
                summary: "Assign governance leads & team",
                order: 500,
                defaultRequirement: WizardStepRequirement.Mandatory);

        // 2. Base Template: All 5 steps are Mandatory
        builder.AddTemplate(
            templateId: "base",
            name: "Base Template",
            description: "Standard 5-step project governance flow.",
            isDefault: true);

        // 3. Internal Fast-Track Template: Disables RFP Timeline, makes Milestone Calendar optional
        builder.AddTemplate(
            templateId: "internal-fast-track",
            name: "Internal Fast-Track",
            description: "Skips RFP Timeline completely; Milestone Calendar is optional.",
            configure: rules => rules
                .Disable(StepIds.RfpTimeline)
                .MakeOptional(StepIds.MilestoneCalendar));

        // 4. Enterprise RFP Template: RFP Timeline is mandatory, Milestones are optional
        builder.AddTemplate(
            templateId: "enterprise-rfp",
            name: "Enterprise RFP",
            description: "Mandatory RFP solicitation window; stage milestones optional.",
            configure: rules => rules
                .MakeMandatory(StepIds.RfpTimeline)
                .MakeOptional(StepIds.MilestoneCalendar));

        // 5. Downstream Extension Hook for Cloning Teams (.Custom.cs)
        ConfigureCustom(builder);

        return builder.Build();
    }

    static partial void ConfigureCustom(WizardFlowBuilder<CreateProjectWizardState> flow);
}
