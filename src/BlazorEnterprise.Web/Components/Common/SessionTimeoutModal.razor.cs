using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Infrastructure.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BlazorEnterprise.Web.Components.Common;

public partial class SessionTimeoutModal : ComponentBase, IAsyncDisposable
{
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUserSessionService SessionService { get; set; } = default!;
    [Inject] private CurrentUserAuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ProtectedSessionStorage SessionStorage { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<SessionTimeoutModal> Logger { get; set; } = default!;

    private bool _showIdleWarning;
    private bool _showMaxLifetimeWarning;
    private int _idleTimeRemainingSeconds;
    private int _maxLifetimeRemainingSeconds;

    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private DateTime _lastHeartbeatUtc = DateTime.UtcNow;
    private DateTime _sessionStartedAtUtc = DateTime.UtcNow;
    private bool _isLoggingOut;

    private PeriodicTimer? _timer;
    private DotNetObjectReference<SessionTimeoutModal>? _dotNetRef;

    protected override void OnInitialized()
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = RunTimeoutWatchdogAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("sessionTracker.init", _dotNetRef);

                var sessionData = await SessionStorage.GetAsync<UserSessionStorageData>("app_session");
                if (sessionData.Success && sessionData.Value?.StartedAtUtc.HasValue == true)
                {
                    _sessionStartedAtUtc = sessionData.Value.StartedAtUtc.Value;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Could not initialize sessionTracker JS listener.");
            }
        }
    }

    private async Task RunTimeoutWatchdogAsync()
    {
        if (_timer is null) return;

        try
        {
            while (await _timer.WaitForNextTickAsync())
            {
                if (!CurrentUser.IsAuthenticated || string.IsNullOrWhiteSpace(CurrentUser.SessionId) || _isLoggingOut)
                {
                    continue;
                }

                var now = DateTime.UtcNow;
                var idleSeconds = (now - _lastActivityUtc).TotalSeconds;
                var sessionSeconds = (now - _sessionStartedAtUtc).TotalSeconds;

                // 1. Check Absolute Maximum 8-Hour Session Lifetime (28,800 seconds)
                if (sessionSeconds >= 28800)
                {
                    await HandleLogout("max_session_timeout");
                    break;
                }
                else if (sessionSeconds >= 27900) // 7 hours 45 minutes
                {
                    _maxLifetimeRemainingSeconds = (int)(28800 - sessionSeconds);
                    if (!_showMaxLifetimeWarning && !_showIdleWarning)
                    {
                        _showMaxLifetimeWarning = true;
                        await InvokeAsync(StateHasChanged);
                    }
                }

                // 2. Check 30-Minute Idle Timeout (1800 seconds total: 15m idle + 15m countdown)
                if (idleSeconds >= 1800)
                {
                    await HandleLogout("idle_timeout");
                    break;
                }
                else if (idleSeconds >= 900) // 15 minutes idle
                {
                    _idleTimeRemainingSeconds = (int)(1800 - idleSeconds);
                    if (!_showIdleWarning)
                    {
                        _showIdleWarning = true;
                    }
                    await InvokeAsync(StateHasChanged);
                }
                else
                {
                    if (_showIdleWarning)
                    {
                        _showIdleWarning = false;
                        await InvokeAsync(StateHasChanged);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Timeout watchdog stopped.");
        }
    }

    [JSInvokable]
    public async Task OnUserActivityDetected()
    {
        _lastActivityUtc = DateTime.UtcNow;

        if (_showIdleWarning)
        {
            _showIdleWarning = false;
            await InvokeAsync(StateHasChanged);
        }

        // Heartbeat renewal: only dispatch to DB if at least 60 seconds have elapsed
        if ((DateTime.UtcNow - _lastHeartbeatUtc).TotalSeconds >= 60 && Guid.TryParse(CurrentUser.SessionId, out var sessionId))
        {
            _lastHeartbeatUtc = DateTime.UtcNow;
            try
            {
                await SessionService.UpdateHeartbeatAsync(sessionId);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Heartbeat renewal failed.");
            }
        }
    }

    private async Task HandleStayLoggedIn()
    {
        _lastActivityUtc = DateTime.UtcNow;
        _showIdleWarning = false;

        if (Guid.TryParse(CurrentUser.SessionId, out var sessionId))
        {
            try
            {
                await SessionService.UpdateHeartbeatAsync(sessionId);
                _lastHeartbeatUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to refresh heartbeat on Stay Logged In.");
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleLogout(string reason)
    {
        if (_isLoggingOut) return;
        _isLoggingOut = true;

        try
        {
            if (Guid.TryParse(CurrentUser.SessionId, out var sessionId))
            {
                await SessionService.RevokeSessionAsync(sessionId, $"Client:{reason}");
            }

            await SessionStorage.DeleteAsync("app_session");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while revoking session during {Reason}", reason);
        }

        if (CurrentUser is AppCurrentUser appUser)
        {
            appUser.IsAuthenticated = false;
            appUser.SessionId = null;
        }

        AuthStateProvider.NotifyUserChanged();
        NavigationManager.NavigateTo($"/login?reason={reason}", forceLoad: true);
    }

    private static string FormatTimeRemaining(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:D2}:{seconds:D2}";
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        try
        {
            if (_dotNetRef != null)
            {
                await JSRuntime.InvokeVoidAsync("sessionTracker.dispose");
                _dotNetRef.Dispose();
            }
        }
        catch { }
    }
}
