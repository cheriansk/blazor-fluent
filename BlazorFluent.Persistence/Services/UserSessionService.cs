using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class UserSessionService : IUserSessionService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        ILogger<UserSessionService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
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
            userEmail?.Contains("@daemon.local", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogCritical("SECURITY ALERT: Blocked illicit attempt to create persistent user session for system daemon '{UserId}' ({UserEmail}).", userId, userEmail);
            throw new InvalidOperationException("System daemon service identities are non-interactive and cannot possess persistent user sessions.");
        }

        var tenantId = _tenantContext.TenantId ?? "default";

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
        var query = dbContext.UserSessions
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(s => s.UserId == userId && !s.IsRevoked);

        if (exceptSessionId.HasValue)
        {
            query = query.Where(s => s.Id != exceptSessionId.Value);
        }

        var sessionsToRevoke = await query.ToListAsync(ct);
        if (sessionsToRevoke.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var session in sessionsToRevoke)
        {
            session.IsRevoked = true;
            session.RevokedAtUtc = now;
            session.RevokedBy = "System (New Login)";
        }

        await dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Revoked {Count} prior active session(s) for user {UserId} upon new login.", sessionsToRevoke.Count, userId);
    }

    public async Task<int> RevokeStaleSessionsAsync(TimeSpan timeout, string revokedBy, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTime.UtcNow - timeout;
        var staleSessions = await dbContext.UserSessions
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(s => !s.IsRevoked && s.LastActivityAtUtc < cutoff)
            .ToListAsync(ct);

        if (staleSessions.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var session in staleSessions)
        {
            session.IsRevoked = true;
            session.RevokedAtUtc = now;
            session.RevokedBy = revokedBy;
        }

        await dbContext.SaveChangesAsync(ct);
        _logger.LogWarning("Admin '{RevokedBy}' revoked {Count} stale sessions older than {Timeout}.",
            revokedBy, staleSessions.Count, timeout);

        return staleSessions.Count;
    }

    public async Task UpdateHeartbeatAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var session = await dbContext.UserSessions
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session != null && !session.IsRevoked)
        {
            session.LastActivityAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> IsSessionRevokedAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var session = await dbContext.UserSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        return session == null || session.IsRevoked;
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, string revokedBy, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var session = await dbContext.UserSessions
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
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
