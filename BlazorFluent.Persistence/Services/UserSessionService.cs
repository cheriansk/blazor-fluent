using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class UserSessionService : IUserSessionService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly HybridCache _cache;
    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        HybridCache cache,
        ILogger<UserSessionService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UserSessionEntity> CreateSessionAsync(
        string userId,
        string userEmail,
        string ipAddress,
        string userAgent,
        CancellationToken ct = default)
    {
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

            var cacheKey = $"revoked_session_{session.Id}";
            await _cache.SetAsync(cacheKey, true, cancellationToken: ct);
        }

        await dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Revoked {Count} prior active session(s) for user {UserId} upon new login.", sessionsToRevoke.Count, userId);
    }

    public async Task<int> RevokeStaleSessionsAsync(TimeSpan timeout, string revokedBy, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTime.UtcNow - timeout;
        var staleSessions = await dbContext.UserSessions
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

            var cacheKey = $"revoked_session_{session.Id}";
            await _cache.SetAsync(cacheKey, true, cancellationToken: ct);
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
        var cacheKey = $"revoked_session_{sessionId}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                await using var dbContext = await _dbFactory.CreateDbContextAsync(token);
                var session = await dbContext.UserSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sessionId, token);

                return session == null || session.IsRevoked;
            },
            cancellationToken: ct);
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, string revokedBy, CancellationToken ct = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);
        var session = await dbContext.UserSessions
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null) return false;

        session.IsRevoked = true;
        session.RevokedAtUtc = DateTime.UtcNow;
        session.RevokedBy = revokedBy;

        await dbContext.SaveChangesAsync(ct);

        var cacheKey = $"revoked_session_{sessionId}";
        await _cache.SetAsync(cacheKey, true, cancellationToken: ct);

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
