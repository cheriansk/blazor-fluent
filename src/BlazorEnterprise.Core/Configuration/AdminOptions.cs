namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for system administration and root tenant management.
/// Section: "Admin"
/// </summary>
public class AdminOptions
{
    public const string SectionName = "Admin";

    public string RootTenantId { get; set; } = string.Empty;
    public string RootTenantSlug { get; set; } = string.Empty;
    public string RootTenantName { get; set; } = string.Empty;
    public string RootAdmins { get; set; } = string.Empty;
    public string AllowedDomain { get; set; } = string.Empty;
    public string KeyVaultUsers { get; set; } = string.Empty;
    public string AuthorizedEmails { get; set; } = string.Empty;
}
