using System;
using System.Security;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Utilities;

/// <summary>
/// Central authority for zero-trust validation and ambient system identity context.
/// Prevents magic fallback strings and hardcoded literals across domain, audit, and persistence layers.
/// </summary>
public static class SystemIdentityUtility
{
    private static ISystemIdentity? _current;

    /// <summary>
    /// Configures the ambient ISystemIdentity provider resolved at host startup.
    /// </summary>
    public static void SetCurrent(ISystemIdentity systemIdentity)
    {
        _current = systemIdentity ?? throw new ArgumentNullException(nameof(systemIdentity));
    }

    /// <summary>
    /// Gets the current ISystemIdentity authority. Fails fast if accessed before host startup.
    /// </summary>
    public static ISystemIdentity Current =>
        _current ?? throw new InvalidOperationException("Zero-Trust Security Violation: Ambient ISystemIdentity has not been initialized.");

    /// <summary>
    /// Evaluates whether the caller is a verified human tenant user (authenticated, non-daemon, and bound to an active tenant).
    /// </summary>
    public static bool IsAuthenticatedTenantUser(ICurrentUser? currentUser, ITenantContext? tenantContext)
    {
        return currentUser is not null &&
               currentUser.IsAuthenticated &&
               !currentUser.IsSystemDaemon &&
               !string.IsNullOrWhiteSpace(currentUser.UserId) &&
               tenantContext is not null &&
               tenantContext.HasActiveTenant &&
               tenantContext.TenantId != Guid.Empty;
    }

    /// <summary>
    /// Strictly verifies that the current execution context is an authenticated human tenant user (!IsSystemDaemon)
    /// with an active tenant context. Throws SecurityException on violation.
    /// </summary>
    public static void RequireAuthenticatedTenantUser(
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        string operationName)
    {
        if (currentUser == null || !currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new SecurityException(
                $"Zero-Trust Security Violation: Operation '{operationName}' requires an authenticated user identity.");
        }

        if (currentUser.IsSystemDaemon)
        {
            throw new SecurityException(
                $"Zero-Trust Security Violation: Operation '{operationName}' is strictly restricted to interactive tenant users and prohibited for SystemDaemon.");
        }

        if (tenantContext == null || !tenantContext.HasActiveTenant || tenantContext.TenantId == Guid.Empty)
        {
            throw new SecurityException(
                $"Zero-Trust Security Violation: Operation '{operationName}' requires an active tenant context.");
        }
    }

    /// <summary>
    /// Strictly verifies that the current execution context contains an authenticated user 
    /// (interactive or daemon) and an active tenant context. Fails fast with InvalidOperationException.
    /// </summary>
    public static void RequireAuthenticatedContext(
        ICurrentUser currentUser, 
        ITenantContext tenantContext, 
        string operationName)
    {
        if (currentUser == null || !currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new InvalidOperationException(
                $"Zero-Trust Security Violation: Operation '{operationName}' requires an established authenticated user identity.");
        }

        if (tenantContext == null || !tenantContext.HasActiveTenant || tenantContext.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Zero-Trust Security Violation: Operation '{operationName}' requires an active tenant context.");
        }
    }

    /// <summary>
    /// Resolves the authenticated user ID for auditing or tracking. Fails fast if context is missing.
    /// </summary>
    public static string ResolveAuditableUserId(ICurrentUser currentUser, string operationName = "Audit")
    {
        if (currentUser == null || !currentUser.IsAuthenticated)
        {
            throw new InvalidOperationException(
                $"Zero-Trust Security Violation: Cannot resolve auditable user ID for '{operationName}'. Context is unauthenticated.");
        }

        return !string.IsNullOrWhiteSpace(currentUser.UserId)
            ? currentUser.UserId
            : (!string.IsNullOrWhiteSpace(currentUser.Email) 
                ? currentUser.Email 
                : throw new InvalidOperationException($"Zero-Trust Security Violation: Authenticated user has neither UserId nor Email for '{operationName}'."));
    }

    /// <summary>
    /// Explicitly initializes an authenticated scoped context for pre-authentication flows 
    /// (e.g. login gate, password verification, seeders) derived from the configured ISystemIdentity.
    /// </summary>
    public static void EstablishPreAuthContext(
        ICurrentUser currentUser, 
        ITenantContext tenantContext, 
        ISystemIdentity systemIdentity,
        string attemptedEmail, 
        string operation = "VerifyLogin")
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(systemIdentity);

        var normalizedEmail = string.IsNullOrWhiteSpace(attemptedEmail) 
            ? $"preauth+{operation.ToLowerInvariant()}@{systemIdentity.RootTenantSlug}.{systemIdentity.SystemDaemonEmailDomain}" 
            : attemptedEmail.Trim();

        currentUser.SetSystemDaemon($"PreAuth:{operation}", normalizedEmail);
        tenantContext.Initialize(
            systemIdentity.RootTenantId, 
            systemIdentity.RootTenantName, 
            UserTypeEnum.CompanyUser, 
            Array.Empty<BlazorFluent.Core.Contracts.TenantInfo>(), 
            systemIdentity.RootTenantSlug,
            isRootTenant: true);
    }

    /// <summary>
    /// Dynamically constructs an internal service identifier for scoped system daemons and workers.
    /// </summary>
    public static string FormatInternalDaemonEmail(string daemonName)
    {
        if (_current != null)
        {
            return _current.FormatInternalDaemonEmail(daemonName);
        }

        return $"service+{daemonName.ToLowerInvariant()}@system.local";
    }

    /// <summary>
    /// Determines whether a given user ID, email, or principal identifier represents an internal system daemon.
    /// </summary>
    public static bool IsSystemDaemon(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return false;

        if (_current != null)
        {
            return _current.IsSystemDaemon(identity);
        }

        var trimmed = identity.Trim();
        return trimmed.StartsWith("daemon:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("daemon@", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("system+", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }
}
