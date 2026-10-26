using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Identity;

/// <summary>
/// Tier 2 Entity Validator for <see cref="UserSessionEntity"/>.
/// Enforces user session tracking and revocation invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class UserSessionEntityValidator : AbstractValidator<UserSessionEntity>
{
    public UserSessionEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required for user session tracking.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.")
            .MaximumLength(200).WithMessage("UserId cannot exceed 200 characters.");

        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IpAddress is required.")
            .MaximumLength(50).WithMessage("IpAddress cannot exceed 50 characters.");

        RuleFor(x => x.StartedAtUtc)
            .NotEmpty().WithMessage("StartedAtUtc must be a valid UTC timestamp.");

        When(x => x.IsRevoked, () =>
        {
            RuleFor(x => x.RevokedAtUtc)
                .NotNull().WithMessage("RevokedAtUtc must be set when session is revoked.");
        });
    }
}
