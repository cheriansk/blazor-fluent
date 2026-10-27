using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Validator for <see cref="ProjectUserRoleEntity"/>.
/// Enforces project-level role assignment invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class ProjectUserRoleEntityValidator : AbstractValidator<ProjectUserRoleEntity>
{
    public ProjectUserRoleEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required for project role assignment.");

        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required for project role assignment.");

        RuleFor(x => x.UserEntityId)
            .NotEmpty().WithMessage("UserEntityId is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid ProjectRole is required.");
    }
}
