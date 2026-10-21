using System;
using System.Collections.Generic;
using System.Linq;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Dtos.Response;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Core.Contracts;

public interface ITenantContext
{
    /// <summary>The active tenant sequential UUIDv7 identifier for the current scope/circuit. Null only before initialization.</summary>
    Guid? TenantId { get; }

    /// <summary>The active tenant's human-friendly vanity URL slug (e.g. 'acme').</summary>
    string? TenantSlug { get; }

    /// <summary>The active tenant's display name.</summary>
    string? TenantName { get; }

    /// <summary>Identifies whether this user is a company employee or a client.</summary>
    UserTypeEnum UserType { get; }

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
    void Initialize(Guid? tenantId, string? tenantName, UserTypeEnum userType,
        IEnumerable<TenantInfo> allowedTenants, bool isHost = false, string? tenantSlug = null);

    /// <summary>
    /// Switches the active tenant by its Guid primary key.
    /// </summary>
    Result SwitchTenant(Guid newTenantId);

    /// <summary>
    /// Switches the active tenant by Guid string or vanity slug.
    /// </summary>
    Result SwitchTenant(string newTenantIdOrSlug);

    /// <summary>
    /// Resets the tenant context to an uninitialized state (e.g., on sign out).
    /// </summary>
    void Reset();
}

/// <summary>
/// Scoped implementation of <see cref="ITenantContext"/>.
/// Defense-in-depth: client users can never switch tenants, even if the call path
/// somehow routes to this method.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private readonly ILogger<TenantContext> _logger;
    private Guid? _tenantId;
    private string? _tenantSlug;
    private string? _tenantName;
    private UserTypeEnum _userType;
    private bool _isHost;
    private List<TenantInfo> _allowedTenants = new();

    public TenantContext(ILogger<TenantContext>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantContext>.Instance;
    }

    public Guid? TenantId => _tenantId;
    public string? TenantSlug => _tenantSlug;
    public string? TenantName => _tenantName;
    public UserTypeEnum UserType => _userType;
    public bool IsHost => _isHost;
    public IReadOnlyList<TenantInfo> AllowedTenants => _allowedTenants.AsReadOnly();

    /// <summary>
    /// Only company users may switch tenants. This is enforced at data level too.
    /// </summary>
    public bool CanSwitchTenant => _userType == UserTypeEnum.CompanyUser;

    public void Initialize(
        Guid? tenantId,
        string? tenantName,
        UserTypeEnum userType,
        IEnumerable<TenantInfo> allowedTenants,
        bool isHost = false,
        string? tenantSlug = null)
    {
        _tenantId = tenantId;
        _tenantSlug = tenantSlug;
        _tenantName = tenantName;
        _userType = userType;
        _isHost = isHost;
        _allowedTenants = new List<TenantInfo>(allowedTenants);

        _logger.LogInformation(
            "Tenant context initialized: TenantId={TenantId}, TenantSlug={TenantSlug}, TenantName={TenantName}, UserType={UserType}, IsHost={IsHost}, AllowedCount={AllowedCount}",
            _tenantId, _tenantSlug, _tenantName, _userType, _isHost, _allowedTenants.Count);
    }

    /// <summary>
    /// Switches the active tenant with fail-closed security by Guid:
    /// 1. ClientUsers are hard-blocked — no exceptions.
    /// 2. The requested tenant must be in the user's AllowedTenants list.
    /// 3. The target tenant must be active.
    /// </summary>
    public Result SwitchTenant(Guid newTenantId)
    {
        if (_userType == UserTypeEnum.ClientUser || newTenantId == Guid.Empty)
        {
            _logger.LogWarning("Security violation: Attempted unauthorized tenant switch to {TargetTenantId}", newTenantId);
            return Result.Failure("Access denied.");
        }

        var target = _allowedTenants.FirstOrDefault(t => t.Id == newTenantId);

        if (target is null)
        {
            _logger.LogWarning(
                "Security violation: User attempted unauthorized switch to tenant {TargetTenantId}. CurrentTenant={CurrentTenantId}",
                newTenantId, _tenantId);
            return Result.Failure("Access denied.");
        }

        if (!target.IsActive)
        {
            _logger.LogWarning("Tenant switch failed: Target tenant {TargetTenantId} is inactive", newTenantId);
            return Result.Failure("This tenant is not currently active.");
        }

        var oldTenantId = _tenantId;
        _tenantId = target.Id;
        _tenantSlug = target.Slug;
        _tenantName = target.Name;

        _logger.LogInformation(
            "Tenant switched successfully from {FromTenantId} to {ToTenantId} for UserType {UserType}",
            oldTenantId, newTenantId, _userType);

        return Result.Success();
    }

    /// <summary>
    /// Switches the active tenant by Guid string or vanity slug.
    /// </summary>
    public Result SwitchTenant(string newTenantIdOrSlug)
    {
        if (string.IsNullOrWhiteSpace(newTenantIdOrSlug) || string.Equals(newTenantIdOrSlug, "default", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Access denied.");
        }

        if (Guid.TryParse(newTenantIdOrSlug, out var parsedGuid))
        {
            return SwitchTenant(parsedGuid);
        }

        var matchBySlug = _allowedTenants.FirstOrDefault(t => string.Equals(t.Slug, newTenantIdOrSlug, StringComparison.OrdinalIgnoreCase));
        if (matchBySlug is not null)
        {
            return SwitchTenant(matchBySlug.Id);
        }

        return Result.Failure("Access denied.");
    }

    public void Reset()
    {
        _tenantId = null;
        _tenantSlug = null;
        _tenantName = null;
        _userType = UserTypeEnum.CompanyUser;
        _isHost = false;
        _allowedTenants.Clear();
        _logger.LogInformation("Tenant context reset.");
    }
}
