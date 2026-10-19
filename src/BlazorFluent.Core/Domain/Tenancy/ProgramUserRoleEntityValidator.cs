using BlazorFluent.Shared.DataListTypes;
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

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.")
            .MaximumLength(200).WithMessage("UserId cannot exceed 200 characters.");

        RuleFor(x => x.UserEmail)
            .NotEmpty().WithMessage("User email is required.")
            .EmailAddress().WithMessage("User email must be a valid email address.")
            .MaximumLength(256).WithMessage("User email cannot exceed 256 characters.");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("User name is required.")
            .MaximumLength(200).WithMessage("User name cannot exceed 200 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid ProgramRole is required.");
    }
}
