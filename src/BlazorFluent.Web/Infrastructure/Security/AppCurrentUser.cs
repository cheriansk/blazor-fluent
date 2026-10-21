using System.Security;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Utilities;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Scoped circuit implementation of <see cref="ICurrentUser"/>.
/// Supports dynamic updates on login, impersonation, and tenant switching.
/// </summary>
public class AppCurrentUser : ICurrentUser
{
    private readonly IRootAdminService _rootAdminService;
    private readonly ITenantContext _tenantContext;
    private string _email = string.Empty;
    private string? _userId;
    private string? _userName;

    public AppCurrentUser(IRootAdminService rootAdminService, ITenantContext tenantContext)
    {
        _rootAdminService = rootAdminService;
        _tenantContext = tenantContext;
    }

    public bool IsAuthenticated { get; set; } = false;

    /// <summary>
    /// Unified Zero-Trust Guardrail: True if and only if the caller is an authenticated human user (!IsSystemDaemon)
    /// AND has an active non-empty tenant initialized in ITenantContext (HasActiveTenant).
    /// </summary>
    public bool IsAuthenticatedTenantUser =>
        IsAuthenticated &&
        !IsSystemDaemon &&
        _tenantContext.HasActiveTenant &&
        !string.IsNullOrWhiteSpace(_userId);

    public string Email
    {
        get => IsAuthenticated
            ? _email
            : throw SecurityViolation(nameof(Email));
        set => _email = value;
    }

    public string UserId
    {
        get => IsAuthenticated
            ? (_userId ?? string.Empty)
            : throw SecurityViolation(nameof(UserId));
        set => _userId = value;
    }

    public string UserName
    {
        get => IsAuthenticated
            ? (_userName ?? string.Empty)
            : throw SecurityViolation(nameof(UserName));
        set => _userName = value;
    }

    private SecurityException SecurityViolation(string memberName)
    {
        return new SecurityException($"Zero-Trust Security Violation: '{memberName}' cannot be accessed on an unauthenticated circuit.");
    }

    /// <summary>
    /// Computes root administrator privileges on-demand.
    /// Strict security: Must be Authenticated, NOT Impersonated, NOT a system daemon,
    /// with non-empty Email matching configured root admins AND operating in the Root Tenant.
    /// Has no setter or mutable backing field.
    /// </summary>
    public bool IsRootAdmin =>
        IsAuthenticatedTenantUser &&
        !IsImpersonated &&
        !string.IsNullOrWhiteSpace(_email) &&
        _rootAdminService.IsRootAdmin(_email) &&
        _tenantContext.IsRootTenant;

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
        _userId = userId;
        _email = email;
        _userName = userName;
        IsAuthenticated = isAuthenticated;
    }

    public void SetImpersonation(string targetUserId, string targetEmail, string targetUserName, string adminId)
    {
        OriginalUserId = _userId;
        OriginalEmail = _email;
        OriginalUserName = _userName;
        _originalRoles.Clear();
        _originalRoles.AddRange(_roles);

        IsImpersonated = true;
        ImpersonatedBy = adminId;
        _userId = targetUserId;
        _email = targetEmail;
        _userName = targetUserName;
        _roles.Clear();
    }

    public void ClearImpersonation(string? adminId = null, string? adminEmail = null, string? adminName = null)
    {
        IsImpersonated = false;
        ImpersonatedBy = null;
        _userId = adminId ?? OriginalUserId;
        _email = adminEmail ?? OriginalEmail ?? string.Empty;
        _userName = adminName ?? OriginalUserName;
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

    public void SetSystemDaemon(string daemonName, string? initiatingUserEmail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(daemonName);

        var resolvedEmail = !string.IsNullOrWhiteSpace(initiatingUserEmail)
            ? initiatingUserEmail
            : SystemIdentityUtility.FormatInternalDaemonEmail(daemonName);

        IsSystemDaemon = true;
        _userId = $"daemon:{daemonName}";
        _userName = daemonName;
        _email = resolvedEmail;
        IsAuthenticated = true;
    }

    public void RestoreUserContext(string userId, string email, string userName, bool isRootAdmin = false)
    {
        _userId = userId;
        _email = email;
        _userName = userName;
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
        _userId = null;
        _email = string.Empty;
        _userName = null;
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
