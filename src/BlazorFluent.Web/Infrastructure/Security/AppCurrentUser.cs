using BlazorFluent.Core.Contracts;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Scoped circuit implementation of <see cref="ICurrentUser"/>.
/// Supports dynamic updates on login, impersonation, and tenant switching.
/// </summary>
public class AppCurrentUser : ICurrentUser
{
    private readonly IRootAdminService _rootAdminService;

    public AppCurrentUser(IRootAdminService rootAdminService)
    {
        _rootAdminService = rootAdminService;
    }

    public string Email { get; set; } = string.Empty;
    public string? UserId { get; set; } = null;
    public string? UserName { get; set; } = null;
    public bool IsAuthenticated { get; set; } = false;

    /// <summary>
    /// Computes root administrator privileges on-demand.
    /// Strict security: Must be Authenticated, NOT Impersonated, with non-empty Email matching configured root admins.
    /// Has no setter or mutable backing field.
    /// </summary>
    public bool IsRootAdmin =>
        IsAuthenticated &&
        !IsImpersonated &&
        !string.IsNullOrWhiteSpace(Email) &&
        _rootAdminService.IsRootAdmin(Email);

    private readonly List<string> _roles = [];

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
    public string? OriginalUserId { get; private set; }
    public string? OriginalEmail { get; private set; }
    public string? OriginalUserName { get; private set; }
    private readonly List<string> _originalRoles = new();

    public void SetUser(string userId, string email, string userName, bool isAuthenticated = true)
    {
        UserId = userId;
        Email = email;
        UserName = userName;
        IsAuthenticated = isAuthenticated;
    }

    public void SetImpersonation(string targetUserId, string targetEmail, string targetUserName, string adminId)
    {
        OriginalUserId = UserId;
        OriginalEmail = Email;
        OriginalUserName = UserName;
        _originalRoles.Clear();
        _originalRoles.AddRange(_roles);

        IsImpersonated = true;
        ImpersonatedBy = adminId;
        UserId = targetUserId;
        Email = targetEmail;
        UserName = targetUserName;
        _roles.Clear();
    }

    public void ClearImpersonation(string? adminId = null, string? adminEmail = null, string? adminName = null)
    {
        IsImpersonated = false;
        ImpersonatedBy = null;
        UserId = adminId ?? OriginalUserId;
        Email = adminEmail ?? OriginalEmail ?? string.Empty;
        UserName = adminName ?? OriginalUserName;
        _roles.Clear();
        if (_originalRoles.Count > 0)
        {
            _roles.AddRange(_originalRoles);
            _originalRoles.Clear();
        }
        OriginalUserId = null;
        OriginalEmail = null;
        OriginalUserName = null;
    }

    public bool IsSystemDaemon { get; private set; }

    public void SetSystemDaemon(string daemonName = "SystemDaemon", string serviceEmail = "daemon@system.local")
    {
        IsSystemDaemon = true;
        UserId = $"daemon:{daemonName}";
        UserName = daemonName;
        Email = serviceEmail;
        IsAuthenticated = true;
    }

    public void RestoreUserContext(string userId, string email, string userName, bool isRootAdmin = false)
    {
        UserId = userId;
        Email = email;
        UserName = userName;
        IsAuthenticated = true;
        IsSystemDaemon = false;
        if (isRootAdmin)
        {
            AddRole("Admin");
            AddRole("RootAdmin");
        }
    }

    public void Clear()
    {
        UserId = null;
        Email = string.Empty;
        UserName = null;
        IsAuthenticated = false;
        IsImpersonated = false;
        ImpersonatedBy = null;
        SessionId = null;
        IsSystemDaemon = false;
        OriginalUserId = null;
        OriginalEmail = null;
        OriginalUserName = null;
        _originalRoles.Clear();
        _roles.Clear();
    }
}
