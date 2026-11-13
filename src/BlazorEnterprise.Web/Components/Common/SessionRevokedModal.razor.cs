using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Infrastructure.Security;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Common;

public partial class SessionRevokedModal : ComponentBase, IDisposable
{
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUserSessionService SessionService { get; set; } = default!;
    [Inject] private CurrentUserAuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SessionRevokedModal> Logger { get; set; } = default!;

    private bool _isRevoked;
    private PeriodicTimer? _timer;
    private readonly CancellationTokenSource _cts = new();

    protected override void OnInitialized()
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = RunRevocationWatchdogAsync();
    }

    private async Task RunRevocationWatchdogAsync()
    {
        if (_timer is null) return;

        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                if (!CurrentUser.IsAuthenticated || string.IsNullOrWhiteSpace(CurrentUser.SessionId))
                    continue;

                if (Guid.TryParse(CurrentUser.SessionId, out var sessionId))
                {
                    var revoked = await SessionService.IsSessionRevokedAsync(sessionId);
                    if (revoked && !_isRevoked)
                    {
                        _isRevoked = true;
                        await InvokeAsync(StateHasChanged);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Session revocation watchdog encountered an unexpected error.");
        }
    }

    private void HandleRedirectToLogin()
    {
        if (CurrentUser is AppCurrentUser appUser)
        {
            appUser.IsAuthenticated = false;
            appUser.SessionId = null;
        }

        AuthStateProvider.NotifyUserChanged();
        NavigationManager.NavigateTo("/login", forceLoad: true);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _timer?.Dispose();
    }
}
