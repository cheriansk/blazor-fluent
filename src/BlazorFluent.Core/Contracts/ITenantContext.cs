using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Dtos.Response;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Core.Contracts;

public interface ITenantContext
{
    /// <summary>
    /// Indicates whether an active tenant session has been established.
    /// Returns false only prior to authentication (e.g. login gate, public health probes).
    /// </summary>
    bool HasActiveTenant { get; }

    /// <summary>
    /// Indicates whether the active tenant is the Root Tenant (00000000-0000-0000-0000-000000000001).
    /// </summary>
    bool IsRootTenant { get; }

    /// <summary>
    /// The active tenant sequential UUIDv7 identifier for the current scope/circuit.
    /// Strictly throws a SecurityException if accessed before initialization (!HasActiveTenant).
    /// Guaranteed non-nullable sequential Guid in all authenticated contexts.
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// Backward-compatible alias to TenantId.
    /// </summary>
    Guid RequiredTenantId => TenantId;

    /// <summary>The active tenant's human-friendly vanity URL slug (e.g. 'root' or 'acme').</summary>
    string TenantSlug { get; }

    /// <summary>The active tenant's display name.</summary>
    string TenantName { get; }

    /// <summary>Identifies whether this user is a company employee or a client.</summary>
    UserTypeEnum UserType { get; }

    /// <summary>
    /// Backward-compatible alias for Root Tenant context.
    /// </summary>
    bool IsHost => IsRootTenant;

    /// <summary>The complete list of tenants this user is authorized to access.</summary>
    IReadOnlyList<TenantInfo> AllowedTenants { get; }

    /// <summary>Whether the current user is allowed to switch their active tenant.</summary>
    bool CanSwitchTenant { get; }

    /// <summary>
    /// Called once at the start of a session to populate context from authenticated claims.
    /// </summary>
    void SetTenant(Guid tenantId, string tenantSlug, string tenantName);

    void Initialize(Guid tenantId, string tenantName, UserTypeEnum userType,
        IEnumerable<TenantInfo> allowedTenants, string tenantSlug);

    /// <summary>
    /// Backward-compatible overload.
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
    private List<TenantInfo> _allowedTenants = new();

    public TenantContext(ILogger<TenantContext>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantContext>.Instance;
    }

    public bool HasActiveTenant => _tenantId.HasValue && _tenantId.Value != Guid.Empty;

    public bool IsRootTenant => HasActiveTenant && _tenantId.Value == IRootAdminService.RootTenantId;

    public Guid TenantId => HasActiveTenant
        ? _tenantId!.Value
        : throw SecurityViolation("TenantId");

    public Guid RequiredTenantId => TenantId;

    public string TenantSlug => HasActiveTenant
        ? (_tenantSlug ?? string.Empty)
        : throw SecurityViolation("TenantSlug");

    public string TenantName => HasActiveTenant
        ? (_tenantName ?? string.Empty)
        : throw SecurityViolation("TenantName");

    public UserTypeEnum UserType => _userType;

    public bool IsHost => IsRootTenant;

    public IReadOnlyList<TenantInfo> AllowedTenants => _allowedTenants.AsReadOnly();

    /// <summary>
    /// Only company users may switch tenants. This is enforced at data level too.
    /// </summary>
    public bool CanSwitchTenant => _userType == UserTypeEnum.CompanyUser;

    private SecurityException SecurityViolation(string memberName)
    {
        _logger.LogCritical("CRITICAL ZERO-TRUST SECURITY VIOLATION: '{MemberName}' accessed on ITenantContext before an active tenant was established.", memberName);
        return new SecurityException($"Zero-Trust Security Violation: '{memberName}' cannot be accessed without an active tenant session.");
    }

    public void Initialize(
        Guid tenantId,
        string tenantName,
        UserTypeEnum userType,
        IEnumerable<TenantInfo> allowedTenants,
        string tenantSlug)
    {
        if (tenantId == Guid.Empty)
        {
            _logger.LogCritical("CRITICAL SECURITY VIOLATION: Attempted to initialize ITenantContext with Guid.Empty.");
            throw new SecurityException("Zero-Trust Security Violation: Cannot initialize tenant context with an empty TenantId.");
        }

        _tenantId = tenantId;
        _tenantSlug = tenantSlug;
        _tenantName = tenantName;
        _userType = userType;
        _allowedTenants = new List<TenantInfo>(allowedTenants);

        _logger.LogInformation(
            "Tenant context initialized: TenantId={TenantId}, TenantSlug={TenantSlug}, TenantName={TenantName}, UserType={UserType}, IsRootTenant={IsRootTenant}, AllowedCount={AllowedCount}",
            _tenantId, _tenantSlug, _tenantName, _userType, IsRootTenant, _allowedTenants.Count);
    }

    public void SetTenant(Guid tenantId, string tenantSlug, string tenantName)
    {
        Initialize(tenantId, tenantName, UserTypeEnum.CompanyUser, [new TenantInfo(tenantId, tenantSlug, tenantName, true)], false, tenantSlug);
    }

    public void Initialize(
        Guid? tenantId,
        string? tenantName,
        UserTypeEnum userType,
        IEnumerable<TenantInfo> allowedTenants,
        bool isHost = false,
        string? tenantSlug = null)
    {
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            _logger.LogCritical("CRITICAL SECURITY VIOLATION: Attempted to initialize ITenantContext with null or empty TenantId.");
            throw new SecurityException("Zero-Trust Security Violation: Cannot initialize tenant context with null or empty TenantId.");
        }

        Initialize(tenantId.Value, tenantName ?? string.Empty, userType, allowedTenants, tenantSlug ?? string.Empty);
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
            _logger.LogCritical("CRITICAL SECURITY VIOLATION: Unauthorized tenant switch attempt to {TargetTenantId} by UserType {UserType}", newTenantId, _userType);
            return Result.Failure("Access denied.");
        }

        var target = _allowedTenants.FirstOrDefault(t => t.Id == newTenantId);

        if (target is null)
        {
            _logger.LogCritical(
                "CRITICAL SECURITY VIOLATION: User attempted unauthorized switch to tenant {TargetTenantId} outside their AllowedTenants. CurrentTenant={CurrentTenantId}",
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
        if (string.IsNullOrWhiteSpace(newTenantIdOrSlug))
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
        _allowedTenants.Clear();
        _logger.LogInformation("Tenant context reset.");
    }
}
