namespace BlazorFluent.Core.Security;

/// <summary>
/// Centralized authorization policy constants across Web and API projects.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Enforces that the user is an authoritative Root Administrator.
    /// </summary>
    public const string HostAdminOnly = "HostAdminOnly";
}
