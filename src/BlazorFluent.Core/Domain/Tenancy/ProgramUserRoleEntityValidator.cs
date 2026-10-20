using FluentValidation;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Validator for <see cref="ProgramUserRoleEntity"/>.
/// Enforces program governance role assignment invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class ProgramUserRoleEntityValidator : AbstractValidator<ProgramUserRoleEntity>
{
    public ProgramUserRoleEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required for program role assignment.");

        RuleFor(x => x.ProgramId)
            .NotEmpty().WithMessage("ProgramId is required for program role assignment.");

        RuleFor(x => x.UserEntityId)
            .NotEmpty().WithMessage("UserEntityId is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid ProgramRole is required.");
    }
}
