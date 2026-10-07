using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Contracts;

public record AuthUserResult(
    UserEntity User,
    bool IsRootAdmin,
    List<TenantInfo> AllowedTenants,
    string ResolvedTenantId,
    string ResolvedTenantName
);

public interface IInternalAuthenticationService
{
    /// <summary>
    /// Verifies user credentials, HMAC integrity seals, and tenant access within an isolated, trusted internal scope.
    /// Returns the verified authentication profile without granting unauthenticated UI circuits raw database access.
    /// </summary>
    Task<Result<AuthUserResult>> VerifyLoginCredentialsAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Rehydrates and re-verifies an existing session from browser storage within an isolated, trusted internal scope.
    /// Re-validates HMAC seals, active dates, account revocation, and session validity against live database records.
    /// </summary>
    Task<Result<AuthUserResult>> VerifyAndRehydrateSessionAsync(string userId, Guid sessionId, CancellationToken ct = default);
}
