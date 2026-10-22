using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Utilities;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class UserSessionService : IUserSessionService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAuditService auditService,
        ILogger<UserSessionService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<UserSessionEntity> CreateSessionAsync(
        string userId,
        string userEmail,
        string ipAddress,
        string userAgent,
        CancellationToken ct = default)
    {
        // Security Gate: Reject persistent session creation for background system daemons
        if (string.Equals(userId, "system", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(userEmail, "system@daemon.local", StringComparison.OrdinalIgnoreCase) ||
            userEmail?.Contains("@daemon.local", StringComparison.OrdinalIgnoreCase) == true ||
            SystemIdentityUtility.IsSystemDaemon(userId) ||
            SystemIdentityUtility.IsSystemDaemon(userEmail))
        {
            _logger.LogCritical("SECURITY ALERT: Blocked illicit attempt to create persistent user session for system daemon '{UserId}' ({UserEmail}).", userId, userEmail);
            throw new InvalidOperationException("System daemon service identities are non-interactive and cannot possess persistent user sessions.");
        }

        // Zero-Trust: Ensure current execution context operates under authentic user identity
        if (!_currentUser.IsAuthenticated)
        {
            _currentUser.RestoreUserContext(userId, userEmail, userEmail);
        }

        var tenantId = _tenantContext.HasActiveTenant ? _tenantContext.TenantId : SystemIdentityUtility.RootTenantId;



        // Enforce strict single-active-session policy per user
        await RevokePreviousSessionsAsync(userId, null, ct);

        var session = new UserSessionEntity
        {
            TenantId = tenantId,
            UserId = userId,
            UserEmail = userEmail,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "Unknown" : userAgent,
            StartedAtUtc = DateTime.UtcNow,
            LastActivityAtUtc = DateTime.UtcNow,
            IsRevoked = false
        };

        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("New user session created for {Email} ({UserId}) from IP {IpAddress}. SessionId: {SessionId}",
            userEmail, userId, ipAddress, session.Id);

        return session;
    }

    public async Task RevokePreviousSessionsAsync(string userId, Guid? exceptSessionId = null, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        var query = dbContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked);

        if (exceptSessionId.HasValue)
        {
            query = query.Where(s => s.Id != exceptSessionId.Value);
        }

        var revokedBy = _currentUser.IsAuthenticated 
            ? (_currentUser.IsAuthenticated ? _currentUser.Email : $"Login:{userId}") 
            : $"Login:{userId}";

        var count = await query.ExecuteUpdateWithAuditedBypassAsync(
            _auditService,
            $"Revoking prior active sessions for user '{userId}' across tenants upon new sign-in.",
            setters => setters
                .SetProperty(s => s.IsRevoked, true)
                .SetProperty(s => s.RevokedAtUtc, now)
                .SetProperty(s => s.RevokedBy, revokedBy),
            ct);

        _logger.LogInformation("Revoked {Count} prior active session(s) for user {UserId} upon new login.", count, userId);
    }

    public async Task<int> RevokeStaleSessionsAsync(TimeSpan timeout, string revokedBy, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTime.UtcNow - timeout;
        var now = DateTime.UtcNow;

        var count = await dbContext.UserSessions
            .Where(s => !s.IsRevoked && s.LastActivityAtUtc < cutoff)
            .ExecuteUpdateWithAuditedBypassAsync(
                _auditService,
                $"Bulk administrative purge of stale user sessions older than '{timeout}'.",
                setters => setters
                    .SetProperty(s => s.IsRevoked, true)
                    .SetProperty(s => s.RevokedAtUtc, now)
                    .SetProperty(s => s.RevokedBy, revokedBy),
                ct);

        _logger.LogWarning("Admin '{RevokedBy}' revoked {Count} stale sessions older than {Timeout}.",
            revokedBy, count, timeout);

        return count;
    }

    public async Task UpdateHeartbeatAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var session = await dbContext.UserSessions
            .AsTracking()
            .Where(s => s.Id == sessionId)
            .FirstOrDefaultWithAuditedBypassAsync(
                _auditService,
                "Updating circuit heartbeat and lifetime evaluation for active user session.",
                ct);
        if (session != null && !session.IsRevoked)
        {
            var now = DateTime.UtcNow;
            if (session.StartedAtUtc.AddHours(8) < now)
            {
                session.IsRevoked = true;
                session.RevokedAtUtc = now;
                session.RevokedBy = "System:MaxSessionLifetimeExceeded";
                _logger.LogWarning("Session heartbeat rejected: session {SessionId} exceeded 8-hour lifetime and was revoked.", sessionId);
            }
            else
            {
                session.LastActivityAtUtc = now;
            }
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> IsSessionRevokedAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var isRevoked = await dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .FirstOrDefaultWithAuditedBypassAsync(
                _auditService,
                "Session active status verification across tenants during circuit heartbeat validation.",
                s => (bool?)s.IsRevoked,
                ct);
        return isRevoked is null or true;
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, string revokedBy, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var session = await dbContext.UserSessions
            .AsTracking()
            .Where(s => s.Id == sessionId)
            .FirstOrDefaultWithAuditedBypassAsync(
                _auditService,
                $"Administrative session revocation initiated by '{revokedBy}'.",
                ct);
        if (session == null) return false;

        session.IsRevoked = true;
        session.RevokedAtUtc = DateTime.UtcNow;
        session.RevokedBy = revokedBy;

        await dbContext.SaveChangesAsync(ct);

        _logger.LogWarning("SECURITY ALERT: User session {SessionId} ({UserEmail}) REVOKED by {RevokedBy}",
            sessionId, session.UserEmail, revokedBy);

        return true;
    }

    public async Task<IReadOnlyList<UserSessionEntity>> GetActiveSessionsAsync(bool includeTerminated = false, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var query = dbContext.UserSessions.AsNoTracking();

        if (!includeTerminated)
        {
            var activeCutoff = DateTime.UtcNow.AddMinutes(-30);
            query = query.Where(s => !s.IsRevoked && s.LastActivityAtUtc >= activeCutoff)
                         .OrderByDescending(s => s.LastActivityAtUtc);
        }
        else
        {
            query = query.OrderByDescending(s => s.StartedAtUtc);
        }

        return await query.Take(200).ToListAsync(ct);
    }
}
