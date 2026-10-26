using System;
using System.Linq;
using BlazorEnterprise.Core.Contracts;
using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Validator for TenantEntity.
/// Enforces single-entity invariants before persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class TenantEntityValidator : AbstractValidator<TenantEntity>
{
    private readonly IRootAdminService? _rootAdminService;

    public TenantEntityValidator(IRootAdminService? rootAdminService = null)
    {
        _rootAdminService = rootAdminService;

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Tenant slug is required.")
            .MaximumLength(100).WithMessage("Tenant slug cannot exceed 100 characters.")
            .Matches(@"^[a-z0-9\-]+$").WithMessage("Tenant slug must be URL-safe (lowercase letters, numbers, and dashes only).");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant Name is required.")
            .MaximumLength(200).WithMessage("Tenant Name cannot exceed 200 characters.");

        RuleFor(x => x.InternalEmailDomains)
            .NotEmpty().WithMessage("At least one Internal email domain is required.")
            .MaximumLength(500).WithMessage("Internal email domains cannot exceed 500 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Tenant subscription StartDate is required.");

        RuleFor(x => x)
            .Must(x =>
            {
                var internals = x.GetInternalDomains();
                return internals.Count >= 1 && internals.Count <= 5;
            })
            .WithMessage("Between 1 and 5 Internal email domains must be configured.");

        // Standard Tenants: Require valid external email domains
        When(x => !IsRootTenant(x), () =>
        {
            RuleFor(x => x.ExternalEmailDomains)
                .NotEmpty().WithMessage("At least one External email domain is required.")
                .MaximumLength(500).WithMessage("External email domains cannot exceed 500 characters.");

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
        });

        // Root Tenant: Strict internal-only governance and administrator domain consistency
        When(x => IsRootTenant(x), () =>
        {
            // 1. Root Tenant must NOT have external email domains
            RuleFor(x => x.ExternalEmailDomains)
                .Must(string.IsNullOrWhiteSpace)
                .WithMessage("Root Tenant cannot have external email domains. Only internal domains are allowed for the system administration tenant.");

            // 2. All root admin emails must match the internal domain(s)
            RuleFor(x => x)
                .Must(tenant =>
                {
                    if (_rootAdminService == null) return true;

                    var adminEmails = _rootAdminService.GetRootAdminEmails();
                    if (adminEmails.Count == 0) return true;

                    var internalDomains = tenant.GetInternalDomains();
                    if (internalDomains.Count == 0) return false;

                    return adminEmails.All(email =>
                    {
                        var emailDomain = ExtractDomain(email);
                        return internalDomains.Any(internalDomain =>
                            internalDomain.Equals(emailDomain, StringComparison.OrdinalIgnoreCase));
                    });
                })
                .WithMessage("All root administrator emails must belong to the Root Tenant's internal email domain(s). All configured root admins must share the same domain.");
        });

        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate.Value.Date >= x.StartDate.Date)
            .WithMessage("Tenant subscription EndDate cannot precede StartDate.");
    }

    private bool IsRootTenant(TenantEntity tenant)
    {
        if (_rootAdminService == null) return false;
        return tenant.Id == _rootAdminService.RootTenantId ||
               string.Equals(tenant.Slug, _rootAdminService.RootTenantSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractDomain(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var atIndex = email.IndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1) return string.Empty;
        return "@" + email.Substring(atIndex + 1).ToLowerInvariant();
    }
}
