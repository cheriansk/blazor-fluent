namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for Rules and Execution Engine connectivity.
/// Section: "Engine"
/// </summary>
public class EngineOptions
{
    public const string SectionName = "Engine";

    public string Mode { get; set; } = "Remote";
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public Dictionary<string, string> TenantLicenseKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetTenantLicenseKey(string tenantId)
    {
        if (TenantLicenseKeys.TryGetValue(tenantId, out var key)) return key;
        return null;
    }
}
