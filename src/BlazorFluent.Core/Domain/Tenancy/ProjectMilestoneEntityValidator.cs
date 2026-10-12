using FluentValidation;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Invariant Validator for ProjectMilestoneEntity.
/// Validates in-memory milestone date boundaries, scope text, and status constraints.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class ProjectMilestoneEntityValidator : AbstractValidator<ProjectMilestoneEntity>
{
    public ProjectMilestoneEntityValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is mandatory for milestones.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Milestone name is mandatory.")
            .MaximumLength(200).WithMessage("Milestone name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Milestone description cannot exceed 2,000 characters.");

        RuleFor(x => x.StartDateUtc)
            .NotEmpty().WithMessage("Start date is mandatory.");

        RuleFor(x => x.EndDateUtc)
            .NotEmpty().WithMessage("End date is mandatory.")
            .Must((milestone, endDate) => endDate >= milestone.StartDateUtc)
            .WithMessage("Milestone target End Date cannot precede Start Date.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid milestone status must be specified.");
    }
}
