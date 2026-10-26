using System;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Persistence implementation of <see cref="IUserPreferencesService"/>,
/// managing user presentation settings within the database boundary.
/// </summary>
public class UserPreferencesService : IUserPreferencesService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<UserPreferencesService> _logger;

    public UserPreferencesService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<UserPreferencesService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<string?> GetUserTimeZoneAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return null;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        return user?.TimeZoneId;
    }

    public async Task SetUserTimeZoneAsync(Guid userId, string timeZoneId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(timeZoneId)) throw new ArgumentException("TimeZoneId is required.", nameof(timeZoneId));

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var user = await db.Users
                .AsTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

            if (user != null)
            {
                user.TimeZoneId = timeZoneId;
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Updated TimeZoneId to '{TimeZoneId}' for user '{UserId}'.", timeZoneId, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist TimeZoneId '{TimeZoneId}' for user '{UserId}'.", timeZoneId, userId);
            throw;
        }
    }
}
