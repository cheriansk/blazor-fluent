namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service managing the system's root super-administrators.
/// Loaded from configuration (semicolon-separated) with a strict maximum of 3 administrators.
/// </summary>
public interface IRootAdminService
{
    /// <summary>
    /// Gets the list of authorized root super-administrator emails (max 3, normalized to lowercase).
    /// </summary>
    IReadOnlyList<string> GetRootAdminEmails();

    /// <summary>
    /// Determines whether the specified email belongs to a designated root super-administrator.
    /// </summary>
    bool IsRootAdmin(string? email);

    /// <summary>
    /// Maximum allowed root administrators in the system.
    /// </summary>
    public const int MaxRootAdminCount = 3;

    /// <summary>
    /// Reserved slug for the internal anchor default tenant.
    /// </summary>
    public const string DefaultTenantSlug = "root";

    /// <summary>
    /// Fixed sequential UUIDv7 identifier for the platform Root Tenant.
    /// </summary>
    public static readonly Guid RootTenantId = new Guid("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Reserved slug for the platform Root Tenant.
    /// </summary>
    public const string RootTenantSlug = "root";

    /// <summary>
    /// Display name for the platform Root Tenant.
    /// </summary>
    public const string RootTenantName = "System Administration";
}
