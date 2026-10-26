using System;
using System.Security;

namespace BlazorEnterprise.Core.Contracts;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>
    /// Non-nullable authenticated UserId. Logs Critical and throws SecurityException if accessed when !IsAuthenticated.
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// Non-nullable authenticated Email. Logs Critical and throws SecurityException if accessed when !IsAuthenticated.
    /// </summary>
    string Email { get; }

    string UserName { get; }
    bool IsInRole(string role);

    // Impersonation & Session Tracking
    bool IsImpersonated { get; }
    string? ImpersonatedBy { get; }
    string? SessionId { get; }

    /// <summary>
    /// Indicates whether the current authenticated user is one of the designated root super-administrators.
    /// Evaluated on-demand with no mutable setter.
    /// </summary>
    bool IsRootAdmin { get; }

    /// <summary>
    /// Indicates whether the current execution context is an internal background daemon / service worker.
    /// </summary>
    bool IsSystemDaemon { get; }

    /// <summary>
    /// Direct access to the active TenantContext for this circuit or execution scope.
    /// </summary>
    ITenantContext TenantContext { get; }

    /// <summary>
    /// Direct access to the active TenantId. Throws SecurityException if accessed when !HasActiveTenant.
    /// </summary>
    Guid TenantId => TenantContext.TenantId;

    /// <summary>
    /// Direct access to the active TenantSlug. Throws SecurityException if accessed when !HasActiveTenant.
    /// </summary>
    string TenantSlug => TenantContext.TenantSlug;

    /// <summary>
    /// 1. True if and only if caller is an authenticated human user in an active, non-Root customer tenant.
    /// Used for all customer workspace operations (Programs, Projects, Tasks, Workflows, Calendar, KnowledgeBase).
    /// </summary>
    bool IsAuthenticated_RegularTenant_User { get; }

    /// <summary>
    /// 2. True if and only if caller is an authenticated human root administrator in the active Root Tenant.
    /// Used strictly for platform administration and tenant catalog management.
    /// </summary>
    bool IsAuthenticated_RootTenant_User { get; }

    /// <summary>
    /// 3. True if and only if caller is an authenticated background daemon operating in an active customer tenant.
    /// Used strictly for single-tenant background batch data processing.
    /// </summary>
    bool IsAuthenticated_RegularTenant_Daemon { get; }

    /// <summary>
    /// 4. True if and only if caller is an authenticated background daemon operating in the active Root Tenant.
    /// Used strictly for platform startup bootstrap and system-wide maintenance jobs.
    /// </summary>
    bool IsAuthenticated_RootTenant_Daemon { get; }

    /// <summary>
    /// Level 1 UI Router Guard: True if and only if caller is an authenticated human user in a regular tenant OR the root tenant.
    /// Daemons are strictly excluded (always false).
    /// </summary>
    bool IsAuthenticatedTenantUser => IsAuthenticated_RegularTenant_User || IsAuthenticated_RootTenant_User;

    /// <summary>
    /// Configures the current scoped context as an internal background system daemon.
    /// Strictly restricted to background workers and seeders.
    /// When initiatingUserEmail is omitted, dynamically resolves an internal service account scoped to the system tenant authority.
    /// </summary>
    void SetSystemDaemon(string daemonName, string? initiatingUserEmail = null);

    /// <summary>
    /// Restores an authenticated user identity into a background execution scope (e.g. queue listeners).
    /// </summary>
    void RestoreUserContext(string userId, string email, string userName, bool isRootAdmin = false);
}

public class DefaultCurrentUser : ICurrentUser
{
    private readonly ITenantContext? _tenantContext;
    private bool _isSystemDaemon;
    private string? _daemonName;
    private string? _daemonEmail;
    private string? _restoredUserId;
    private string? _restoredEmail;
    private string? _restoredUserName;
    private bool _restoredIsRootAdmin;

    public DefaultCurrentUser(ITenantContext? tenantContext = null)
    {
        _tenantContext = tenantContext;
    }

    public bool IsAuthenticated => _isSystemDaemon || _restoredUserId != null;

    public string Email => IsAuthenticated
        ? (_restoredEmail ?? _daemonEmail ?? string.Empty)
        : throw new SecurityException("Zero-Trust Security Violation: Email cannot be accessed on an unauthenticated context.");

    public string UserId => IsAuthenticated
        ? (_restoredUserId ?? (_isSystemDaemon ? $"daemon:{_daemonName}" : string.Empty))
        : throw new SecurityException("Zero-Trust Security Violation: UserId cannot be accessed on an unauthenticated context.");

    public string UserName => IsAuthenticated
        ? (_restoredUserName ?? _daemonName ?? string.Empty)
        : throw new SecurityException("Zero-Trust Security Violation: UserName cannot be accessed on an unauthenticated context.");

    public bool IsInRole(string role) => false;

    public bool IsImpersonated => false;
    public string? ImpersonatedBy => null;
    public string? SessionId => null;
    public bool IsRootAdmin => _restoredIsRootAdmin;
    public bool IsSystemDaemon => _isSystemDaemon;

    public ITenantContext TenantContext => _tenantContext ?? throw new InvalidOperationException("ITenantContext is not available on DefaultCurrentUser.");

    public Guid TenantId => TenantContext.TenantId;
    public string TenantSlug => TenantContext.TenantSlug;

    public bool IsAuthenticated_RegularTenant_User =>
        IsAuthenticated && !_isSystemDaemon && (_tenantContext?.HasActiveTenant == true) && !_tenantContext.IsRootTenant;

    public bool IsAuthenticated_RootTenant_User =>
        IsAuthenticated && !_isSystemDaemon && _restoredIsRootAdmin && (_tenantContext?.HasActiveTenant == true) && _tenantContext.IsRootTenant;

    public bool IsAuthenticated_RegularTenant_Daemon =>
        IsAuthenticated && _isSystemDaemon && (_tenantContext?.HasActiveTenant == true) && !_tenantContext.IsRootTenant;

    public bool IsAuthenticated_RootTenant_Daemon =>
        IsAuthenticated && _isSystemDaemon && (_tenantContext?.HasActiveTenant == true) && _tenantContext.IsRootTenant;

    public bool IsAuthenticatedTenantUser => IsAuthenticated_RegularTenant_User || IsAuthenticated_RootTenant_User;

    public void SetSystemDaemon(string daemonName, string? initiatingUserEmail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(daemonName);

        var resolvedEmail = !string.IsNullOrWhiteSpace(initiatingUserEmail)
            ? initiatingUserEmail
            : Utilities.SystemIdentityUtility.FormatInternalDaemonEmail(daemonName);

        _isSystemDaemon = true;
        _daemonName = daemonName;
        _daemonEmail = resolvedEmail;
    }

    public void RestoreUserContext(string userId, string email, string userName, bool isRootAdmin = false)
    {
        _restoredUserId = userId;
        _restoredEmail = email;
        _restoredUserName = userName;
        _restoredIsRootAdmin = isRootAdmin;
    }
}
