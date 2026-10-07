namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Encrypted session payload stored in browser sessionStorage via ProtectedSessionStorage.
/// Preserves session ID, active tenant, and active project across page refreshes (F5) within the same browser session.
/// </summary>
public record UserSessionStorageData(
    Guid SessionId,
    string UserId,
    string Email,
    string ActiveTenantId,
    Guid? ActiveProjectId = null,
    string? TimeZoneId = null);
