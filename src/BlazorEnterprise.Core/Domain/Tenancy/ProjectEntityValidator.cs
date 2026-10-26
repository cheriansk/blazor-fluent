using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Validator for ProjectEntity.
/// Enforces single-entity invariants before persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class ProjectEntityValidator : AbstractValidator<ProjectEntity>
{
    public ProjectEntityValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Project description cannot exceed 1000 characters.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Project TenantId is mandatory.");

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Project due date cannot precede start date.");

        When(x => x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate!.Value)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("Project target end date cannot precede start date.");
        });

        When(x => x.RevisedDueDate.HasValue, () =>
        {
            RuleFor(x => x.RevisedDueDate!.Value)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("Project revised due date cannot precede start date.");
        });
    }
}
