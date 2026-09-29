namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Encrypted session payload stored in browser sessionStorage via ProtectedSessionStorage.
/// Preserves session ID and active tenant across page refreshes (F5) within the same browser session.
/// </summary>
public record UserSessionStorageData(
    Guid SessionId,
    string UserId,
    string Email,
    string ActiveTenantId);
