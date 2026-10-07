using BlazorFluent.Core.Contracts;
using BlazorFluent.Infrastructure.Security;
using BlazorFluent.Persistence.Context;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Services;

/// <summary>
/// Scoped circuit service managing user timezone preferences and date/time conversions.
/// Seamlessly loads and persists preference across browser sessions and database.
/// </summary>
public class UserTimeZoneService : IUserTimeZoneService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<UserTimeZoneService> _logger;

    private string _timeZoneId = "UTC";
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;
    private bool _initialized;

    public event Action? OnChange;

    public string TimeZoneId => _timeZoneId;
    public TimeZoneInfo TimeZone => _timeZone;

    public IReadOnlyList<TimeZoneInfo> AvailableTimeZones { get; }

    public UserTimeZoneService(
        ProtectedSessionStorage sessionStorage,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ICurrentUser currentUser,
        ILogger<UserTimeZoneService> logger)
    {
        _sessionStorage = sessionStorage;
        _dbContextFactory = dbContextFactory;
        _currentUser = currentUser;
        _logger = logger;

        try
        {
            AvailableTimeZones = TimeZoneInfo.GetSystemTimeZones().OrderBy(tz => tz.BaseUtcOffset).ToList();
        }
        catch
        {
            AvailableTimeZones = new List<TimeZoneInfo> { TimeZoneInfo.Utc };
        }
    }

    public void ApplyUserTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return;
        ApplyTimeZone(timeZoneId);
        _initialized = true;
        OnChange?.Invoke();
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            // 1. Try reading from ProtectedSessionStorage first (keeps active circuit state)
            var sessionResult = await _sessionStorage.GetAsync<UserSessionStorageData>("app_session");
            if (sessionResult.Success && sessionResult.Value != null && !string.IsNullOrWhiteSpace(sessionResult.Value.TimeZoneId))
            {
                ApplyTimeZone(sessionResult.Value.TimeZoneId);
                _initialized = true;
                OnChange?.Invoke();
                return;
            }

            // 2. If user is authenticated, query persisted UserEntity.TimeZoneId
            if (_currentUser.IsAuthenticated && Guid.TryParse(_currentUser.UserId, out var userId))
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null && !string.IsNullOrWhiteSpace(user.TimeZoneId))
                {
                    ApplyTimeZone(user.TimeZoneId);
                    _initialized = true;
                    OnChange?.Invoke();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not initialize user timezone from storage/database. Defaulting to UTC.");
        }

        // Only seal initialization if user is authenticated; otherwise allow rehydration to populate
        if (_currentUser.IsAuthenticated)
        {
            ApplyTimeZone("UTC");
            _initialized = true;
        }
        else
        {
            ApplyTimeZone("UTC");
        }
    }

    public async Task SetTimeZoneAsync(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZoneId = "UTC";
        }

        ApplyTimeZone(timeZoneId);

        // 1. Persist to protected session storage
        try
        {
            var sessionResult = await _sessionStorage.GetAsync<UserSessionStorageData>("app_session");
            if (sessionResult.Success && sessionResult.Value != null)
            {
                await _sessionStorage.SetAsync("app_session", sessionResult.Value with
                {
                    TimeZoneId = _timeZoneId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist timezone {TimeZoneId} to session storage.", _timeZoneId);
        }

        // 2. Persist to database if authenticated
        if (_currentUser.IsAuthenticated && Guid.TryParse(_currentUser.UserId, out var userId))
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var user = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    user.TimeZoneId = _timeZoneId;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist timezone {TimeZoneId} to database for user {UserId}.", _timeZoneId, userId);
                throw;
            }
        }

        _initialized = true;
        OnChange?.Invoke();
    }

    public DateTime? ToUserTime(DateTime? utcDateTime)
    {
        if (!utcDateTime.HasValue) return null;
        return ToUserTime(utcDateTime.Value);
    }

    public DateTime ToUserTime(DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind switch
        {
            DateTimeKind.Utc => utcDateTime,
            DateTimeKind.Local => utcDateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
    }

    public DateTime ToUtc(DateTime userDateTime)
    {
        if (userDateTime.Kind == DateTimeKind.Utc)
            return userDateTime;

        var unspecified = DateTime.SpecifyKind(userDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZone);
    }

    public string FormatDateTime(DateTime? dateTime, string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (!dateTime.HasValue) return "—";
        var userTime = ToUserTime(dateTime.Value);
        return userTime.ToString(format);
    }

    public string FormatDate(DateTime? dateTime, string format = "yyyy-MM-dd")
    {
        if (!dateTime.HasValue) return "—";
        var userTime = ToUserTime(dateTime.Value);
        return userTime.ToString(format);
    }

    private void ApplyTimeZone(string timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            _timeZone = tz;
            _timeZoneId = tz.Id;
        }
        catch
        {
            _timeZone = TimeZoneInfo.Utc;
            _timeZoneId = "UTC";
        }
    }
}
