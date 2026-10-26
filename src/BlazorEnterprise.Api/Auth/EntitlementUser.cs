namespace BlazorEnterprise.Api.Auth;

/// <summary>
/// User entitlement record determining federated login authorization and subscription validity.
/// </summary>
public class EntitlementUser
{
    public string Email { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = ["User"];
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsValidForLogin(DateTime asOfUtc)
    {
        if (!IsActive) return false;
        var date = asOfUtc.Date;
        return StartDate.Date <= date && date <= EndDate.Date;
    }
}
