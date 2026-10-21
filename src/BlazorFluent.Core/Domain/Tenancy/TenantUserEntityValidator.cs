using FluentValidation;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Validator for <see cref="TenantUserEntity"/>.
/// Enforces multi-tenant user membership invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class TenantUserEntityValidator : AbstractValidator<TenantUserEntity>
{
    public TenantUserEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required for tenant user membership.");

        RuleFor(x => x.UserEntityId)
            .NotEmpty().WithMessage("UserEntityId is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid TenantRole is required.");
    }
}
