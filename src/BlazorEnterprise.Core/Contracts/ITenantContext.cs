using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Core.Contracts;

public record TenantInfo(Guid Id, string Slug, string Name, bool IsActive);

/// <summary>
/// Scoped ambient context representing the currently authenticated tenant.
/// Enforces Zero-Slug Relational Integrity (TenantId: Guid) and fail-closed tenant scoping.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Indicates whether a valid tenant context has been initialized for the current scope/circuit.
    /// </summary>
    bool HasActiveTenant { get; }

    /// <summary>
    /// Indicates whether the active tenant is the Root Tenant.
    /// </summary>
    bool IsRootTenant { get; }

    /// <summary>
    /// The active tenant sequential UUIDv7 identifier for the current scope/circuit.
    /// Strictly throws a SecurityException if accessed before initialization (!HasActiveTenant).
    /// </summary>
    Guid TenantId { get; }

    /// <summary>The active tenant's human-friendly vanity URL slug.</summary>
    string TenantSlug { get; }

    /// <summary>The active tenant's display name.</summary>
    string TenantName { get; }

    /// <summary>Identifies whether this user is a company employee or a client.</summary>
    UserTypeEnum UserType { get; }

    /// <summary>The complete list of tenants this user is authorized to access.</summary>
    IReadOnlyList<TenantInfo> AllowedTenants { get; }

    /// <summary>Whether the current user is allowed to switch their active tenant.</summary>
    bool CanSwitchTenant { get; }

    /// <summary>
    /// Called once at the start of a session to populate context from authenticated claims.
    /// </summary>
    void SetTenant(Guid tenantId, string tenantSlug, string tenantName);

    /// <summary>
    /// Initializes the tenant context for the active scope.
    /// </summary>
    void Initialize(Guid tenantId, string tenantName, UserTypeEnum userType,
        IEnumerable<TenantInfo> allowedTenants, string tenantSlug, bool isRootTenant = false);

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
    private readonly IRootAdminService? _rootAdminService;
    private readonly ILogger<TenantContext> _logger;
    private Guid? _tenantId;
    private string? _tenantSlug;
    private string? _tenantName;
    private UserTypeEnum _userType;
    private bool _isExplicitRootTenant;
    private List<TenantInfo> _allowedTenants = new();

    public TenantContext(IRootAdminService? rootAdminService = null, ILogger<TenantContext>? logger = null)
    {
        _rootAdminService = rootAdminService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantContext>.Instance;
    }

    public bool HasActiveTenant => _tenantId.HasValue && _tenantId.Value != Guid.Empty;

    public bool IsRootTenant => HasActiveTenant && (
        _rootAdminService != null
            ? _tenantId.Value == _rootAdminService.RootTenantId
            : _isExplicitRootTenant);

    public Guid TenantId => HasActiveTenant
        ? _tenantId!.Value
        : throw SecurityViolation("TenantId");

    public string TenantSlug => HasActiveTenant
        ? (_tenantSlug ?? string.Empty)
        : throw SecurityViolation("TenantSlug");

    public string TenantName => HasActiveTenant
        ? (_tenantName ?? string.Empty)
        : throw SecurityViolation("TenantName");

    public UserTypeEnum UserType => _userType;

    public IReadOnlyList<TenantInfo> AllowedTenants => _allowedTenants.AsReadOnly();

    public bool CanSwitchTenant =>
        _userType == UserTypeEnum.CompanyUser && _allowedTenants.Count > 1;

    public void SetTenant(Guid tenantId, string tenantSlug, string tenantName)
    {
        if (tenantId == Guid.Empty)
        {
            _logger.LogCritical("CRITICAL SECURITY VIOLATION: Attempted to set ITenantContext with Guid.Empty.");
            throw new SecurityException("Zero-Trust Security Violation: Cannot set tenant context with Guid.Empty.");
        }

        _tenantId = tenantId;
        _tenantSlug = tenantSlug;
        _tenantName = tenantName;
        _isExplicitRootTenant = _rootAdminService != null && tenantId == _rootAdminService.RootTenantId;

        _logger.LogInformation(
            "Tenant context updated via SetTenant: TenantId={TenantId}, TenantSlug={TenantSlug}, TenantName={TenantName}",
            _tenantId, _tenantSlug, _tenantName);
    }

    public void Initialize(
        Guid tenantId,
        string tenantName,
        UserTypeEnum userType,
        IEnumerable<TenantInfo> allowedTenants,
        string tenantSlug,
        bool isRootTenant = false)
    {
        if (tenantId == Guid.Empty)
        {
            _logger.LogCritical("CRITICAL SECURITY VIOLATION: Attempted to initialize ITenantContext with Guid.Empty.");
            throw new SecurityException("Zero-Trust Security Violation: Cannot initialize tenant context with an empty TenantId.");
        }

        _tenantId = tenantId;
        _tenantName = tenantName;
        _userType = userType;
        _tenantSlug = tenantSlug;
        _allowedTenants = allowedTenants?.ToList() ?? new();
        _isExplicitRootTenant = isRootTenant || (_rootAdminService != null && tenantId == _rootAdminService.RootTenantId);

        _logger.LogInformation(
            "Tenant context initialized: TenantId={TenantId}, TenantSlug={TenantSlug}, TenantName={TenantName}, UserType={UserType}, IsRootTenant={IsRootTenant}, AllowedCount={AllowedCount}",
            _tenantId, _tenantSlug, _tenantName, _userType, IsRootTenant, _allowedTenants.Count);
    }

    public Result SwitchTenant(Guid newTenantId)
    {
        if (_userType != UserTypeEnum.CompanyUser)
        {
            _logger.LogWarning("Security violation: ClientUser attempted to switch tenants to {TargetTenantId}", newTenantId);
            return Result.Failure("Client users cannot access other tenants.");
        }

        var target = _allowedTenants.FirstOrDefault(t => t.Id == newTenantId);
        if (target is null)
        {
            _logger.LogWarning("Unauthorized tenant switch attempt to {TargetTenantId} by user with allowed tenants: {Allowed}",
                newTenantId, string.Join(",", _allowedTenants.Select(t => t.Id)));
            return Result.Failure("You do not have access to that tenant.");
        }

        _tenantId = target.Id;
        _tenantSlug = target.Slug;
        _tenantName = target.Name;
        _isExplicitRootTenant = _rootAdminService != null && target.Id == _rootAdminService.RootTenantId;

        _logger.LogInformation("Tenant switched to: {TenantName} ({TenantId}, {TenantSlug})", target.Name, target.Id, target.Slug);
        return Result.Success();
    }

    public Result SwitchTenant(string newTenantIdOrSlug)
    {
        if (Guid.TryParse(newTenantIdOrSlug, out var targetGuid))
        {
            return SwitchTenant(targetGuid);
        }

        if (_userType != UserTypeEnum.CompanyUser)
        {
            _logger.LogWarning("Security violation: ClientUser attempted to switch tenants by slug to {TargetSlug}", newTenantIdOrSlug);
            return Result.Failure("Client users cannot access other tenants.");
        }

        var target = _allowedTenants.FirstOrDefault(t => string.Equals(t.Slug, newTenantIdOrSlug, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            _logger.LogWarning("Unauthorized tenant switch attempt by slug '{TargetSlug}' by user with allowed tenants: {Allowed}",
                newTenantIdOrSlug, string.Join(",", _allowedTenants.Select(t => t.Slug)));
            return Result.Failure("You do not have access to that tenant.");
        }

        _tenantId = target.Id;
        _tenantSlug = target.Slug;
        _tenantName = target.Name;
        _isExplicitRootTenant = _rootAdminService != null && target.Id == _rootAdminService.RootTenantId;

        _logger.LogInformation("Tenant switched to: {TenantName} ({TenantId}, {TenantSlug})", target.Name, target.Id, target.Slug);
        return Result.Success();
    }

    public void Reset()
    {
        _tenantId = null;
        _tenantSlug = null;
        _tenantName = null;
        _isExplicitRootTenant = false;
        _allowedTenants.Clear();
        _logger.LogInformation("Tenant context reset.");
    }

    private SecurityException SecurityViolation(string memberName)
    {
        _logger.LogCritical("CRITICAL SECURITY VIOLATION: Attempted to access ITenantContext.{MemberName} before tenant initialization.", memberName);
        return new SecurityException($"Zero-Trust Security Violation: Cannot access ITenantContext.{memberName} before tenant context has been initialized.");
    }
}
