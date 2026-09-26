using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Top-level organizational unit. A tenant owns one or more projects.
/// Not tenant-scoped itself — only lives in host/admin context.
/// </summary>
public class TenantEntity : AuditableEntity, IGlobalEntity
{
    /// <summary>Short, URL-safe slug used to resolve tenants (e.g., from subdomain or header).</summary>
    public string Slug { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Navigation: all projects belonging to this tenant.</summary>
    public ICollection<ProjectEntity> Projects { get; set; } = new List<ProjectEntity>();
}
