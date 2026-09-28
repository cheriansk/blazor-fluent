using BlazorFluent.Core.Contracts;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Scoped circuit implementation of <see cref="ICurrentUser"/>.
/// Supports dynamic updates on login, impersonation, and tenant switching.
/// </summary>
public class AppCurrentUser : ICurrentUser
{
    public string Email { get; set; } = "admin@blazorfluent.local";
    public string? UserId { get; set; } = "dev_admin";
    public string? UserName { get; set; } = "System Administrator";
    public bool IsAuthenticated { get; set; } = true;

    private readonly List<string> _roles = ["Admin", "User"];

    public bool IsInRole(string role) => _roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public void AddRole(string role)
    {
        if (!_roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            _roles.Add(role);
    }

    public void SetRoles(IEnumerable<string> roles)
    {
        _roles.Clear();
        _roles.AddRange(roles);
    }

    // Impersonation & Session tracking
    public bool IsImpersonated { get; set; }
    public string? ImpersonatedBy { get; set; }
    public string? SessionId { get; set; }

    public void SetUser(string userId, string email, string userName, bool isAuthenticated = true)
    {
        UserId = userId;
        Email = email;
        UserName = userName;
        IsAuthenticated = isAuthenticated;
    }

    public void SetImpersonation(string targetUserId, string targetEmail, string targetUserName, string adminId)
    {
        IsImpersonated = true;
        ImpersonatedBy = adminId;
        UserId = targetUserId;
        Email = targetEmail;
        UserName = targetUserName;
    }

    public void ClearImpersonation(string adminId, string adminEmail, string adminName)
    {
        IsImpersonated = false;
        ImpersonatedBy = null;
        UserId = adminId;
        Email = adminEmail;
        UserName = adminName;
    }
}
