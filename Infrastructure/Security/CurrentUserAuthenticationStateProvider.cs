using System.Security.Claims;
using BlazorFluent.Core.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Bridges <see cref="ICurrentUser"/> to Blazor's <see cref="AuthenticationStateProvider"/>.
/// Ensures &lt;AuthorizeRouteView&gt; and cascading authentication states receive
/// the current user claims and authentication status.
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
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _currentUser.UserId ?? "dev_user"),
            new Claim(ClaimTypes.Name, _currentUser.UserName ?? "Developer"),
            new Claim(ClaimTypes.Email, _currentUser.Email ?? "dev@example.com")
        }, "ApplicationAuth");

        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }
}
