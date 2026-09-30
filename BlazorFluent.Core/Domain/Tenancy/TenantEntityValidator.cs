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

        RuleFor(x => x.InternalEmailDomains)
            .NotEmpty().WithMessage("At least one Internal email domain is required.")
            .MaximumLength(500).WithMessage("Internal email domains cannot exceed 500 characters.");

        RuleFor(x => x.ExternalEmailDomains)
            .NotEmpty().WithMessage("At least one External email domain is required.")
            .MaximumLength(500).WithMessage("External email domains cannot exceed 500 characters.");

        RuleFor(x => x)
            .Must(x =>
            {
                var internals = x.GetInternalDomains();
                return internals.Count >= 1 && internals.Count <= 5;
            })
            .WithMessage("Between 1 and 5 Internal email domains must be configured.");

        RuleFor(x => x)
            .Must(x =>
            {
                var externals = x.GetExternalDomains();
                return externals.Count >= 1 && externals.Count <= 5;
            })
            .WithMessage("Between 1 and 5 External email domains must be configured.");

        RuleFor(x => x)
            .Must(x =>
            {
                var internals = x.GetInternalDomains();
                var externals = x.GetExternalDomains();
                return !internals.Intersect(externals, StringComparer.OrdinalIgnoreCase).Any();
            })
            .WithMessage("Internal and External email domains must not overlap. No domain can exist in both lists.");

        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate.Value >= x.StartDate)
            .WithMessage("Tenant subscription EndDate cannot precede StartDate.");
    }
}
