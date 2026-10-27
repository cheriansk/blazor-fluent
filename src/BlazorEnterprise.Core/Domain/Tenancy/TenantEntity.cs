using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// Top-level organizational unit. A tenant owns one or more projects.
/// Not tenant-scoped itself — lives in host/admin context.
/// Implements IEffectiveDatedEntity for subscription/contract validity.
/// </summary>
public class TenantEntity : AuditableEntity, IGlobalEntity, IEffectiveDatedEntity, ISoftDeletableEntity
{
    /// <summary>Short, URL-safe slug used to resolve tenants (e.g., from subdomain or header). Immutable after creation.</summary>
    public required string Slug { get; set; }

    public required string Name { get; set; }

    /// <summary>Semicolon-separated domain suffixes for internal staff (up to 5, e.g. "@microsoft.com;@msft.com"). Immutable after creation.</summary>
    public string? InternalEmailDomains { get; set; }

    /// <summary>Semicolon-separated domain suffixes for client/external users (up to 5, e.g. "@salesforce.com"). Immutable after creation.</summary>
    public string? ExternalEmailDomains { get; set; }

    /// <summary>Backward-compatible helper returning the primary internal domain.</summary>
    public string InternalEmailDomain => GetInternalDomains().FirstOrDefault() ?? string.Empty;

    /// <summary>Backward-compatible helper returning the primary external domain.</summary>
    public string ExternalEmailDomain => GetExternalDomains().FirstOrDefault() ?? string.Empty;

    /// <summary>Returns parsed and normalized list of internal email domains (e.g. "@domain.com").</summary>
    public IReadOnlyList<string> GetInternalDomains() =>
        string.IsNullOrWhiteSpace(InternalEmailDomains)
            ? []
            : InternalEmailDomains.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeDomain)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    /// <summary>Returns parsed and normalized list of external email domains (e.g. "@domain.com").</summary>
    public IReadOnlyList<string> GetExternalDomains() =>
        string.IsNullOrWhiteSpace(ExternalEmailDomains)
            ? []
            : ExternalEmailDomains.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeDomain)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string NormalizeDomain(string domain)
    {
        var trimmed = domain.Trim().ToLowerInvariant();
        return trimmed.StartsWith("@") ? trimmed : "@" + trimmed;
    }

    /// <summary>Operational status switch. Setting to false immediately deactivates the tenant. Required.</summary>
    public bool IsActive { get; set; }

    /// <summary>Contract or subscription start date. Required.</summary>
    public required DateTime StartDate { get; set; }

    /// <summary>Contract or subscription end date. Optional.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Navigation: all programs belonging to this tenant.</summary>
    public ICollection<ProgramEntity> Programs { get; set; } = new List<ProgramEntity>();

    /// <summary>Navigation: all projects belonging to this tenant.</summary>
    public ICollection<ProjectEntity> Projects { get; set; } = new List<ProjectEntity>();

    /// <summary>Navigation: all user memberships in this tenant.</summary>
    public ICollection<TenantUserEntity> TenantUsers { get; set; } = new List<TenantUserEntity>();

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
