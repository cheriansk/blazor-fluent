using BlazorEnterprise.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Infrastructure.Security;

namespace BlazorEnterprise.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private CurrentUserAuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ProtectedSessionStorage SessionStorage { get; set; } = default!;
    [Inject] private IUserSessionService SessionService { get; set; } = default!;
    [Inject] private ICircuitSessionTracker CircuitTracker { get; set; } = default!;
    [Inject] private IInternalAuthenticationService AuthVerificationService { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Logger { get; set; } = default!;
    [Inject] private INavigationStateService NavState { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;

    private bool HasActiveTenantSession => CurrentUser.IsAuthenticatedTenantUser;

    private bool IsAnonymousPage
    {
        get
        {
            var uri = NavigationManager.ToBaseRelativePath(NavigationManager.Uri).ToLowerInvariant();
            return uri.StartsWith("login") || uri.StartsWith("auth-error") || uri.StartsWith("error") || uri.StartsWith("not-found");
        }
    }

    protected override void OnInitialized()
    {
        NavState.OnChange += HandleNavStateChanged;
        TimeZoneService.OnChange += HandleTimeZoneChanged;
    }

    private void HandleNavStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NavState.OnChange -= HandleNavStateChanged;
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await TimeZoneService.InitializeAsync();

        if (!CurrentUser.IsAuthenticated)
        {
            try
            {
                var result = await SessionStorage.GetAsync<UserSessionStorageData>("app_session");
                if (result.Success && result.Value != null)
                {
                    var data = result.Value;

                    var rehydrateResult = await AuthVerificationService.VerifyAndRehydrateSessionAsync(data.UserId, data.SessionId);
                    if (rehydrateResult.IsSuccess)
                    {
                        var verified = rehydrateResult.Value!;
                        var user = verified.User;
                        var isHost = verified.IsRootAdmin;

                        if (CurrentUser is AppCurrentUser appUser)
                        {
                            appUser.SetUser(user.Id.ToString(), user.Email, user.FullName, isAuthenticated: true);
                            appUser.SessionId = data.SessionId.ToString();
                            if (isHost)
                            {
                                appUser.AddRole("Admin");
                                appUser.AddRole("RootAdmin");
                            }
                        }

                        // Apply persisted user timezone immediately upon rehydration
                        var tzToApply = !string.IsNullOrWhiteSpace(user.TimeZoneId)
                            ? user.TimeZoneId
                            : data.TimeZoneId;
                        if (!string.IsNullOrWhiteSpace(tzToApply))
                        {
                            TimeZoneService.ApplyUserTimeZone(tzToApply);
                        }

                        // Prefer the intra-session active tenant if still permitted in allowed tenants
                        var resolvedTenantId = verified.ResolvedTenantId;
                        var resolvedTenantSlug = verified.ResolvedTenantSlug;
                        var resolvedTenantName = verified.ResolvedTenantName;

                        if (!string.IsNullOrWhiteSpace(data.ActiveTenantId))
                        {
                            var activeMatch = verified.AllowedTenants.FirstOrDefault(t => t.Id.ToString() == data.ActiveTenantId || t.Slug == data.ActiveTenantId);
                            if (activeMatch != null)
                            {
                                resolvedTenantId = activeMatch.Id;
                                resolvedTenantSlug = activeMatch.Slug;
                                resolvedTenantName = activeMatch.Name;
                            }
                        }

                        TenantContext.Initialize(
                            tenantId: resolvedTenantId,
                            tenantName: resolvedTenantName,
                            userType: user.UserType,
                            allowedTenants: verified.AllowedTenants,
                            tenantSlug: resolvedTenantSlug);

                        if (data.ActiveProjectId.HasValue)
                        {
                            ProjectContext.Initialize(data.ActiveProjectId.Value, null, null, Array.Empty<ProjectInfoDto>());
                        }

                        AuthStateProvider.NotifyUserChanged();
                        StateHasChanged();

                        // If user is currently on an anonymous/login page, navigate back to returnUrl or home
                        if (IsAnonymousPage)
                        {
                            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
                            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                            if (queryParams.TryGetValue("returnUrl", out var returnUrl) && !string.IsNullOrWhiteSpace(returnUrl))
                            {
                                NavigationManager.NavigateTo(returnUrl.ToString(), replace: true);
                            }
                            else
                            {
                                NavigationManager.NavigateTo("/", replace: true);
                            }
                        }
                        return;
                    }
                    else
                    {
                        Logger.LogWarning("Session restoration rejected: {Error}", rehydrateResult.Error);
                        await SessionStorage.DeleteAsync("app_session");
                        if (!IsAnonymousPage)
                        {
                            NavigationManager.NavigateTo($"/auth-error?reason={Uri.EscapeDataString(rehydrateResult.Error ?? "Account access denied.")}", replace: true);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to restore session from browser sessionStorage.");
            }

            if (!IsAnonymousPage)
            {
                NavigationManager.NavigateTo("/login");
            }
        }
        else if ((!TenantContext.HasActiveTenant || TenantContext.TenantId == Guid.Empty) && !IsAnonymousPage)
        {
            NavigationManager.NavigateTo("/login", replace: true);
        }
    }

    private async Task SignOut()
    {
        try
        {
            var result = await SessionStorage.GetAsync<UserSessionStorageData>("app_session");
            if (result.Success && result.Value != null)
            {
                var adminOrSelf = CurrentUser.IsAuthenticated ? CurrentUser.UserId : (result.Value.UserId ?? "user");
                await SessionService.RevokeSessionAsync(result.Value.SessionId, adminOrSelf);
            }
            await SessionStorage.DeleteAsync("app_session");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while cleaning session storage on sign out.");
        }

        if (CurrentUser is AppCurrentUser appUser)
        {
            appUser.Clear();
        }

        TenantContext.Reset();
        ProjectContext.Reset();
        AuthStateProvider.NotifyUserChanged();
        NavigationManager.NavigateTo("/login", forceLoad: true);
    }
}
