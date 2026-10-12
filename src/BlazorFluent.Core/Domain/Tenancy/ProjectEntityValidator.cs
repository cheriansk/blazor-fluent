using FluentValidation;

namespace BlazorFluent.Core.Domain.Tenancy;

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

        RuleFor(x => x.TenantEntityId)
            .NotEmpty().WithMessage("Project TenantEntityId foreign key must not be empty.");
    }
}
