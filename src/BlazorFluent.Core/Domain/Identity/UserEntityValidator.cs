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

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("Time zone ID is required.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("User account access StartDate is required.");

        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate.Value.Date >= x.StartDate.Date)
            .WithMessage("User account access EndDate cannot precede StartDate.");
    }
}
