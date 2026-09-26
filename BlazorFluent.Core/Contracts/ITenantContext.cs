using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Contracts;

public interface ITenantContext
{
    /// <summary>The active tenant ID for the current scope/circuit. Null only before initialization.</summary>
    string? TenantId { get; }

    /// <summary>The active tenant's display name.</summary>
    string? TenantName { get; }

    /// <summary>Identifies whether this user is a company employee or a client.</summary>
    UserType UserType { get; }

    /// <summary>
    /// True for host/superadmin: bypasses all tenant query filters.
    /// A host user can see all tenants' data. Never set this for a client user.
    /// </summary>
    bool IsHost { get; }

    /// <summary>The complete list of tenants this user is authorized to access.</summary>
    IReadOnlyList<TenantInfo> AllowedTenants { get; }

    /// <summary>Whether the current user is allowed to switch their active tenant.</summary>
    bool CanSwitchTenant { get; }

    /// <summary>
    /// Called once at the start of a session to populate context from authenticated claims.
    /// </summary>
    void Initialize(string? tenantId, string? tenantName, UserType userType,
        IEnumerable<TenantInfo> allowedTenants, bool isHost = false);

    /// <summary>
    /// Switches the active tenant. Company users may switch to any of their AllowedTenants.
    /// Client users are strictly prohibited and this will always return a security failure.
    /// </summary>
    Result SwitchTenant(string newTenantId);
}

/// <summary>
/// Scoped implementation of <see cref="ITenantContext"/>.
/// Defense-in-depth: client users can never switch tenants, even if the call path
/// somehow routes to this method.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private string? _tenantId;
    private string? _tenantName;
    private UserType _userType;
    private bool _isHost;
    private List<TenantInfo> _allowedTenants = new();

    public string? TenantId => _tenantId;
    public string? TenantName => _tenantName;
    public UserType UserType => _userType;
    public bool IsHost => _isHost;
    public IReadOnlyList<TenantInfo> AllowedTenants => _allowedTenants.AsReadOnly();

    /// <summary>
    /// Only company users may switch tenants. This is enforced at data level too.
    /// </summary>
    public bool CanSwitchTenant => _userType == UserType.CompanyUser;

    public void Initialize(
        string? tenantId,
        string? tenantName,
        UserType userType,
        IEnumerable<TenantInfo> allowedTenants,
        bool isHost = false)
    {
        _tenantId = tenantId;
        _tenantName = tenantName;
        _userType = userType;
        _isHost = isHost;
        _allowedTenants = new List<TenantInfo>(allowedTenants);
    }

    /// <summary>
    /// Switches the active tenant with fail-closed security:
    /// 1. ClientUsers are hard-blocked — no exceptions.
    /// 2. The requested tenant must be in the user's AllowedTenants list.
    /// 3. The target tenant must be active.
    /// </summary>
    public Result SwitchTenant(string newTenantId)
    {
        if (_userType == UserType.ClientUser)
        {
            // Security: never expose that this path exists for client users.
            return Result.Failure("Access denied.");
        }

        var target = _allowedTenants.FirstOrDefault(t => t.Id == newTenantId);

        if (target is null)
        {
            return Result.Failure("Access denied.");
        }

        if (!target.IsActive)
        {
            return Result.Failure("This tenant is not currently active.");
        }

        _tenantId = target.Id;
        _tenantName = target.Name;
        return Result.Success();
    }
}