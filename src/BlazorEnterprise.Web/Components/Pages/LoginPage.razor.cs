using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Utilities;
using BlazorEnterprise.Infrastructure.Security;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorEnterprise.Web.Components.Pages;

public partial class LoginPage : ComponentBase
{
    [Inject] private IInternalAuthenticationService AuthVerificationService { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = default!;
    [Inject] private IOptions<DevLoginOptions> DevLoginOptions { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IRootAdminService RootAdminService { get; set; } = default!;
    [Inject] private IUserSessionService SessionService { get; set; } = default!;
    [Inject] private IAuditService AuditService { get; set; } = default!;
    [Inject] private ProtectedSessionStorage SessionStorage { get; set; } = default!;
    [Inject] private CurrentUserAuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<LoginPage> Logger { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "reason")]
    public string? Reason { get; set; }

    private bool _isAcknowledged;
    private bool _isLoading;
    private string _localLoginEmail = string.Empty;
    private string? _errorMessage;
    private string? _infoMessage;
    private string _secretKey = string.Empty;

    private bool _isSelectingTenant;
    private AuthUserResult? _pendingAuth;
    private string? _selectedTenantSlug;

    protected override void OnInitialized()
    {
        if (HostEnvironment.IsDevelopment() && DevLoginOptions.Value.Enabled)
        {
            Serilog.Log.Fatal("FATAL SECURITY WARNING: Local passwordless email login is rendered on LoginPage. Remove or disable 'Auth:DevLogin:Enabled' before production!");
        }

        if (!string.IsNullOrWhiteSpace(Reason))
        {
            _infoMessage = Reason switch
            {
                "idle_timeout" => "Your session has expired due to 30 minutes of inactivity. Please sign in again.",
                "max_session_timeout" => "Your session has reached the maximum 8-hour limit. Please sign in again.",
                _ => null
            };
        }

        _secretKey = Configuration["Security:IntegritySecret"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_secretKey) || _secretKey.StartsWith("__SET_VIA_"))
        {
            Logger.LogError("CRITICAL SECURITY ERROR: 'Security:IntegritySecret' is missing or unconfigured.");
        }
    }

    private void HandleMicrosoftSignIn()
    {
        if (!_isAcknowledged) return;

        var clientId = Configuration["AzureAd:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _errorMessage = "Entra ID (Azure AD) is not configured for this environment. Please configure 'AzureAd:ClientId'.";
            return;
        }

        NavigationManager.NavigateTo("/authentication/challenge", forceLoad: true);
    }

    private async Task HandleLocalEmailSignInAsync()
    {
        if (!_isAcknowledged || string.IsNullOrWhiteSpace(_localLoginEmail))
        {
            return;
        }

        var email = _localLoginEmail.Trim();
        Serilog.Log.Fatal("FATAL SECURITY WARNING: Passwordless local email login invoked for '{Email}'. Remove or disable 'Auth:DevLogin:Enabled' before production!", email);

        await TestSignInAsync(email);
    }

    private async Task TestSignInAsync(string email)
    {
        if (SystemIdentityUtility.IsSystemDaemon(email))
        {
            Logger.LogCritical("CRITICAL SECURITY VIOLATION: Login attempt by system daemon identity '{Email}' rejected.", email);
            _errorMessage = "System daemon identities are restricted to background processing and can never log in.";
            return;
        }

        _isLoading = true;
        _errorMessage = null;

        try
        {
            var authResult = await AuthVerificationService.VerifyLoginCredentialsAsync(email);
            if (!authResult.IsSuccess)
            {
                NavigationManager.NavigateTo($"/auth-error?reason={Uri.EscapeDataString(authResult.Error ?? "Authentication failed.")}");
                return;
            }

            await ProcessAuthSuccessAsync(authResult.Value!);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during test login for {Email}", email);
            _errorMessage = "An unexpected error occurred during login.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ProcessAuthSuccessAsync(AuthUserResult verified)
    {
        if (SystemIdentityUtility.IsSystemDaemon(verified.User.Email) || SystemIdentityUtility.IsSystemDaemon(verified.User.Id.ToString()))
        {
            Logger.LogCritical("CRITICAL SECURITY VIOLATION: Login attempt by system daemon identity '{Email}' rejected.", verified.User.Email);
            _errorMessage = "System daemon identities are restricted to background processing and can never log in.";
            return;
        }

        if (verified.AllowedTenants.Count == 0)
        {
            _errorMessage = "Your account is not assigned to any active organization. Please contact an administrator.";
            return;
        }

        if (verified.AllowedTenants.Count == 1)
        {
            var singleTenant = verified.AllowedTenants[0];
            await CompleteLoginAsync(verified, singleTenant.Id, singleTenant.Name, singleTenant.Slug);
            return;
        }

        _pendingAuth = verified;
        _selectedTenantSlug = null;
        _isSelectingTenant = true;
    }

    private async Task HandleConfirmTenantSelectionAsync()
    {
        if (_pendingAuth is null || string.IsNullOrWhiteSpace(_selectedTenantSlug)) return;

        var selected = _pendingAuth.AllowedTenants.FirstOrDefault(t => t.Id.ToString() == _selectedTenantSlug || t.Slug == _selectedTenantSlug);
        if (selected == null) return;

        await CompleteLoginAsync(_pendingAuth, selected.Id, selected.Name, selected.Slug);
    }

    private void CancelTenantSelection()
    {
        _isSelectingTenant = false;
        _pendingAuth = null;
        _selectedTenantSlug = null;
        _errorMessage = null;
    }

    private async Task CompleteLoginAsync(AuthUserResult verified, Guid tenantId, string tenantName, string tenantSlug)
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            var user = verified.User;
            var isRootTenant = tenantId == RootAdminService.RootTenantId || string.Equals(tenantSlug, RootAdminService.RootTenantSlug, StringComparison.OrdinalIgnoreCase);

            TenantContext.Initialize(
                tenantId: tenantId,
                tenantName: tenantName,
                userType: user.UserType,
                allowedTenants: verified.AllowedTenants,
                tenantSlug: tenantSlug);

            if (CurrentUser is AppCurrentUser appUser)
            {
                appUser.SetUser(user.Id.ToString(), user.Email, user.FullName, isAuthenticated: true);
                if (verified.IsRootAdmin && isRootTenant)
                {
                    appUser.AddRole("Admin");
                    appUser.AddRole("RootAdmin");
                }
            }

            var session = await SessionService.CreateSessionAsync(
                user.Id.ToString(),
                user.Email,
                "127.0.0.1 (Local Dev)",
                "Edge / Windows 11");

            if (CurrentUser is AppCurrentUser authenticatedUser)
            {
                authenticatedUser.SessionId = session.Id.ToString();
            }

            try
            {
                await SessionStorage.SetAsync("app_session", new UserSessionStorageData(
                    session.Id,
                    user.Id.ToString(),
                    user.Email,
                    tenantId.ToString(),
                    TimeZoneId: user.TimeZoneId,
                    StartedAtUtc: session.StartedAtUtc));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not persist session to browser sessionStorage.");
            }

            if (!string.IsNullOrWhiteSpace(user.TimeZoneId))
            {
                TimeZoneService.ApplyUserTimeZone(user.TimeZoneId);
            }

            await AuditService.LogUserActivityAsync(
                "UserLoginSuccess",
                $"User '{user.Email}' logged in successfully into tenant '{tenantName}' ({tenantId}). SessionId: {session.Id}");

            AuthStateProvider.NotifyUserChanged();

            NavigationManager.NavigateTo("/");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error completing login for {Email}", verified.User.Email);
            _errorMessage = "An unexpected error occurred establishing your session.";
        }
        finally
        {
            _isLoading = false;
        }
    }
}
