namespace BlazorEnterprise.Web.Components.Common;

/// <summary>
/// Helper for generating standardized, safe user-facing error messages paired with Correlation IDs.
/// Strictly adheres to the Zero Sensitive Error Leakage and On-Screen Reference ID laws in AGENTS.md.
/// </summary>
public static class UserFacingError
{
    public static string For(string operation, string? correlationId = null)
    {
        var refToken = !string.IsNullOrWhiteSpace(correlationId) ? correlationId : "ambient";
        return $"Unable to {operation}. Please try again or share Reference ID: '{refToken}' with your administrator.";
    }
}
