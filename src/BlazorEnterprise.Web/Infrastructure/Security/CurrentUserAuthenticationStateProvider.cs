using System.Security.Claims;
using BlazorEnterprise.Core.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorEnterprise.Infrastructure.Security;

/// <summary>
/// Bridges <see cref="ICurrentUser"/> and <see cref="ITenantContext"/> to Blazor's <see cref="AuthenticationStateProvider"/>.
/// Level 1 Defense: Only issues an authenticated ClaimsPrincipal when <see cref="ICurrentUser.IsAuthenticatedTenantUser"/> is true
/// (authenticated human user, not a SystemDaemon, and bound to an active non-empty TenantId).
/// </summary>
public class CurrentUserAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;

    public CurrentUserAuthenticationStateProvider(ICurrentUser currentUser, ITenantContext tenantContext)
    {
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Level 1 Zero-Trust Gate: Unauthenticated sessions, sessions without an active tenant,
        // and background SystemDaemons are never issued an authenticated UI ClaimsPrincipal.
        if (!_currentUser.IsAuthenticatedTenantUser)
        {
            var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(unauthenticated));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _currentUser.UserId),
            new(ClaimTypes.Name, _currentUser.UserName),
            new(ClaimTypes.Email, _currentUser.Email),
            new("tenant_id", _tenantContext.TenantId.ToString()),
            new("tenant_slug", _tenantContext.TenantSlug)
        };

        if (_currentUser.IsRootAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "RootAdmin"));
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }
        else if (_currentUser.IsInRole("Admin"))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        if (_currentUser.IsImpersonated)
        {
            claims.Add(new Claim("IsImpersonated", "true"));
            claims.Add(new Claim("ImpersonatedBy", _currentUser.ImpersonatedBy ?? string.Empty));
        }

        var identity = new ClaimsIdentity(claims, "ApplicationAuth");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }

    public void NotifyUserChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
