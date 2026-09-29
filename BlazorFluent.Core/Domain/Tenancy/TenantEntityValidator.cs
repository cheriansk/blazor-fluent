using FluentValidation;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Validator for TenantEntity.
/// Enforces single-entity invariants before persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class TenantEntityValidator : AbstractValidator<TenantEntity>
{
    public TenantEntityValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Tenant slug is required.")
            .MaximumLength(100).WithMessage("Tenant slug cannot exceed 100 characters.")
            .Matches(@"^[a-z0-9\-]+$").WithMessage("Tenant slug must be URL-safe (lowercase letters, numbers, and dashes only).");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Tenant Code is required.")
            .MaximumLength(15).WithMessage("Tenant Code cannot exceed 15 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant Name is required.")
            .MaximumLength(200).WithMessage("Tenant Name cannot exceed 200 characters.");

        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate.Value >= x.StartDate)
            .WithMessage("Tenant subscription EndDate cannot precede StartDate.");
    }
}
