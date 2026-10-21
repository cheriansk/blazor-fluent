using FluentValidation;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Tier 2 Entity Validator for <see cref="ImpersonationGrantEntity"/>.
/// Enforces Zero-Trust impersonation grant lifecycle, expiration, and audit invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class ImpersonationGrantEntityValidator : AbstractValidator<ImpersonationGrantEntity>
{
    public ImpersonationGrantEntityValidator()
    {
        RuleFor(x => x.SourceAdminId)
            .NotEmpty().WithMessage("SourceAdminId is required.")
            .MaximumLength(200).WithMessage("SourceAdminId cannot exceed 200 characters.");

        RuleFor(x => x.SourceAdminEmail)
            .NotEmpty().WithMessage("SourceAdminEmail is required.")
            .EmailAddress().WithMessage("SourceAdminEmail must be a valid email address.")
            .MaximumLength(256).WithMessage("SourceAdminEmail cannot exceed 256 characters.");

        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("TargetUserId is required.")
            .MaximumLength(200).WithMessage("TargetUserId cannot exceed 200 characters.");

        RuleFor(x => x.TargetUserEmail)
            .NotEmpty().WithMessage("TargetUserEmail is required.")
            .EmailAddress().WithMessage("TargetUserEmail must be a valid email address.")
            .MaximumLength(256).WithMessage("TargetUserEmail cannot exceed 256 characters.");

        RuleFor(x => x.TargetTenantId)
            .NotEmpty().WithMessage("TargetTenantId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A clear reason is required for administrative impersonation.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");

        RuleFor(x => x.ExpiresAtUtc)
            .GreaterThan(x => x.StartedAtUtc)
            .WithMessage("ExpiresAtUtc must be after StartedAtUtc.");

        When(x => x.IsRevoked, () =>
        {
            RuleFor(x => x.RevokedAtUtc)
                .NotNull().WithMessage("RevokedAtUtc must be set when impersonation grant is revoked.");
        });
    }
}
