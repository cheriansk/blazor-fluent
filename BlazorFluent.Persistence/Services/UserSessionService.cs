using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class UserSessionService : IUserSessionService
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly HybridCache _cache;
    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(
        AppDbContext dbContext,
        ITenantContext tenantContext,
        HybridCache cache,
        ILogger<UserSessionService> logger)
    {
        _dbContext = dbContext;
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

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("New user session created for {Email} ({UserId}) from IP {IpAddress}. SessionId: {SessionId}",
            userEmail, userId, ipAddress, session.Id);

        return session;
    }

    public async Task UpdateHeartbeatAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session != null && !session.IsRevoked)
        {
            session.LastActivityAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> IsSessionRevokedAsync(Guid sessionId, CancellationToken ct = default)
    {
        var cacheKey = $"revoked_session_{sessionId}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                var session = await _dbContext.UserSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sessionId, token);

                return session == null || session.IsRevoked;
            },
            cancellationToken: ct);
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, string revokedBy, CancellationToken ct = default)
    {
        var session = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null) return false;

        session.IsRevoked = true;
        session.RevokedAtUtc = DateTime.UtcNow;
        session.RevokedBy = revokedBy;

        await _dbContext.SaveChangesAsync(ct);

        var cacheKey = $"revoked_session_{sessionId}";
        await _cache.SetAsync(cacheKey, true, cancellationToken: ct);

        _logger.LogWarning("SECURITY ALERT: User session {SessionId} ({UserEmail}) REVOKED by {RevokedBy}",
            sessionId, session.UserEmail, revokedBy);

        return true;
    }

    public async Task<IReadOnlyList<UserSessionEntity>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        var query = _dbContext.UserSessions
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAtUtc);

        return await query.Take(100).ToListAsync(ct);
    }
}
