using System.Security.Claims;
using BlazorFluent.Core.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Bridges <see cref="ICurrentUser"/> to Blazor's <see cref="AuthenticationStateProvider"/>.
/// Ensures &lt;AuthorizeRouteView&gt; and cascading authentication states receive
/// dynamic user claims and respond to login, logout, and impersonation events.
/// </summary>
public class CurrentUserAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ICurrentUser _currentUser;

    public CurrentUserAuthenticationStateProvider(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_currentUser.IsAuthenticated)
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(anonymous));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _currentUser.UserId ?? "anonymous"),
            new(ClaimTypes.Name, _currentUser.UserName ?? "User"),
            new(ClaimTypes.Email, _currentUser.Email)
        };

        if (_currentUser.IsInRole("Admin"))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        if (_currentUser.IsImpersonated)
        {
            claims.Add(new Claim("IsImpersonated", "true"));
            claims.Add(new Claim("ImpersonatedBy", _currentUser.ImpersonatedBy ?? ""));
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
