using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Contracts;

public interface IUserSessionService
{
    /// <summary>
    /// Creates a new user session record in PostgreSQL and registers it in HybridCache.
    /// </summary>
    Task<UserSessionEntity> CreateSessionAsync(
        string userId,
        string userEmail,
        string ipAddress,
        string userAgent,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the last activity timestamp for an active session.
    /// </summary>
    Task UpdateHeartbeatAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the given session has been revoked by an administrator.
    /// Uses HybridCache for ultra-fast (nanosecond) lookups.
    /// </summary>
    Task<bool> IsSessionRevokedAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Revokes an active session immediately, broadcasting invalidation across all nodes.
    /// </summary>
    Task<bool> RevokeSessionAsync(Guid sessionId, string revokedBy, CancellationToken ct = default);

    /// <summary>
    /// Revokes any existing active sessions for a specific user to enforce single-active-session policy.
    /// </summary>
    Task RevokePreviousSessionsAsync(string userId, Guid? exceptSessionId = null, CancellationToken ct = default);

    /// <summary>
    /// Revokes all stale sessions whose last activity is older than the specified timeout.
    /// Returns the number of sessions revoked.
    /// </summary>
    Task<int> RevokeStaleSessionsAsync(TimeSpan timeout, string revokedBy, CancellationToken ct = default);

    /// <summary>
    /// Returns active user sessions. If includeTerminated is true, includes expired and revoked sessions.
    /// </summary>
    Task<IReadOnlyList<UserSessionEntity>> GetActiveSessionsAsync(bool includeTerminated = false, CancellationToken ct = default);
}
