using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsSessionsTab : ComponentBase, IDisposable
{
    [Inject] private IUserSessionService SessionService { get; set; } = default!;
    [Inject] private ICircuitSessionTracker CircuitTracker { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IAuditService AuditService { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private ILogger<SettingsSessionsTab> Logger { get; set; } = default!;

    private enum SessionStatusFilter
    {
        ActiveAndIdle,
        OnlineOnly,
        IdleOnly,
        TerminatedOnly,
        All
    }

    private SessionStatusFilter _currentFilter = SessionStatusFilter.ActiveAndIdle;
    private bool _isLoading = true;
    private bool _isError;
    private string? _statusMessage;
    private List<UserSessionEntity> _sessions = new();
    private string _sessionSearchText = string.Empty;

    private int ActiveAndIdleCount => _sessions.Count(s => !IsTerminated(s));
    private int OnlineCount => _sessions.Count(s => GetSessionStatus(s) == "Online");
    private int IdleCount => _sessions.Count(s => GetSessionStatus(s) == "Idle");
    private int TerminatedCount => _sessions.Count(s => IsTerminated(s));

    private bool IsTerminated(UserSessionEntity s) =>
        s.IsRevoked || (DateTime.UtcNow - s.LastActivityAtUtc > TimeSpan.FromMinutes(30));

    private string GetSessionStatus(UserSessionEntity session)
    {
        if (session.IsRevoked) return "Revoked";

        var idleDuration = DateTime.UtcNow - session.LastActivityAtUtc;
        if (idleDuration > TimeSpan.FromMinutes(30)) return "Expired";

        var isConnected = CircuitTracker.IsSessionConnected(session.Id);
        if (isConnected && idleDuration <= TimeSpan.FromMinutes(15)) return "Online";
        if (isConnected && idleDuration > TimeSpan.FromMinutes(15)) return "Idle";
        if (!isConnected && idleDuration <= TimeSpan.FromMinutes(30)) return "Idle";

        return "Disconnected";
    }

    private IQueryable<UserSessionEntity> FilteredSessions
    {
        get
        {
            var query = _sessions.AsQueryable();

            query = _currentFilter switch
            {
                SessionStatusFilter.ActiveAndIdle => query.Where(s => !IsTerminated(s)),
                SessionStatusFilter.OnlineOnly => query.Where(s => GetSessionStatus(s) == "Online"),
                SessionStatusFilter.IdleOnly => query.Where(s => GetSessionStatus(s) == "Idle"),
                SessionStatusFilter.TerminatedOnly => query.Where(s => IsTerminated(s)),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(_sessionSearchText))
            {
                var term = _sessionSearchText.Trim();
                query = query.Where(s =>
                    (s.UserEmail != null && s.UserEmail.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (s.UserId != null && s.UserId.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (s.TenantId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (s.IpAddress != null && s.IpAddress.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (s.UserAgent != null && s.UserAgent.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            return query;
        }
    }

    private void SetFilter(SessionStatusFilter filter)
    {
        _currentFilter = filter;
        StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
        TimeZoneService.OnChange += HandleTimeZoneChanged;

        if (!CurrentUser.IsRootAdmin)
        {
            _isLoading = false;
            _isError = true;
            _statusMessage = "Access Denied: Active user session administration is restricted exclusively to Root Administrators.";
            return;
        }

        await LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        if (!CurrentUser.IsRootAdmin) return;

        _isLoading = true;
        _statusMessage = null;

        try
        {
            var sessions = await SessionService.GetActiveSessionsAsync(includeTerminated: true);
            _sessions = sessions.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load active user sessions");
            _statusMessage = "Failed to load sessions.";
            _isError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RevokeAllStaleAsync()
    {
        if (!CurrentUser.IsRootAdmin) return;

        var adminId = CurrentUser.IsAuthenticated ? CurrentUser.UserId : "admin";
        try
        {
            var count = await SessionService.RevokeStaleSessionsAsync(TimeSpan.FromMinutes(30), adminId);
            if (count > 0)
            {
                await AuditService.LogSecurityEventAsync(
                    "StaleSessionsRevoked",
                    AuditSeverityEnum.Warning,
                    $"Admin '{adminId}' revoked {count} stale session(s) older than 30 minutes.");

                _statusMessage = $"Successfully cleaned up {count} stale session(s).";
                _isError = false;
            }
            else
            {
                _statusMessage = "No stale sessions (>30m inactive) found to revoke.";
                _isError = false;
            }

            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error revoking stale sessions");
            _statusMessage = "Failed to revoke stale sessions.";
            _isError = true;
        }
    }

    private async Task RevokeSessionAsync(Guid sessionId, string userEmail)
    {
        if (!CurrentUser.IsRootAdmin) return;

        var adminId = CurrentUser.IsAuthenticated ? CurrentUser.UserId : "admin";
        var success = await SessionService.RevokeSessionAsync(sessionId, adminId);
        if (success)
        {
            await AuditService.LogSecurityEventAsync(
                "SessionRevokedByAdmin",
                AuditSeverityEnum.Warning,
                $"Admin '{CurrentUser.Email}' revoked active session '{sessionId}' for user '{userEmail}'.");

            _statusMessage = $"Successfully revoked session for '{userEmail}'. Real-time invalidation broadcast.";
            _isError = false;
            await LoadSessionsAsync();
        }
        else
        {
            _statusMessage = "Could not revoke session.";
            _isError = true;
        }
    }

    private string GetRelativeTime(DateTime dt)
    {
        var span = DateTime.UtcNow - dt.ToUniversalTime();
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return TimeZoneService.FormatDateTime(dt, "MMM dd HH:mm");
    }

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
    }
}
