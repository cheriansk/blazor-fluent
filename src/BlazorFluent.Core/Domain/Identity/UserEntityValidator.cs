using FluentValidation;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Tier 2 Entity Validator for UserEntity.
/// Enforces single-entity invariants before persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class UserEntityValidator : AbstractValidator<UserEntity>
{
    public UserEntityValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("User email is required.")
            .MaximumLength(256).WithMessage("User email cannot exceed 256 characters.")
            .EmailAddress().WithMessage("User email format is invalid.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("User full name is required.")
            .MaximumLength(200).WithMessage("User full name cannot exceed 200 characters.");

        RuleFor(x => x)
            .Must(x => x.EndDateUtc >= x.StartDateUtc)
            .WithMessage("User account access EndDateUtc cannot precede StartDateUtc.");
    }
}
