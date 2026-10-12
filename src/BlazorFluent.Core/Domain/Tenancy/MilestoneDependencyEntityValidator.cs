using FluentValidation;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Invariant Validator for MilestoneDependencyEntity.
/// Validates in-memory milestone predecessor relationships and self-dependency constraints.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class MilestoneDependencyEntityValidator : AbstractValidator<MilestoneDependencyEntity>
{
    public MilestoneDependencyEntityValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is mandatory for milestone dependencies.");

        RuleFor(x => x.MilestoneId)
            .NotEmpty().WithMessage("MilestoneId is mandatory.");

        RuleFor(x => x.DependsOnMilestoneId)
            .NotEmpty().WithMessage("DependsOnMilestoneId is mandatory.");

        RuleFor(x => x)
            .Must(x => x.MilestoneId != x.DependsOnMilestoneId)
            .WithMessage("A milestone cannot depend on itself.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Milestone dependency notes cannot exceed 1,000 characters.");
    }
}
