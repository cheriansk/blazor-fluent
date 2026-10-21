using System;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Utilities;

/// <summary>
/// Central authority for system identity semantics, zero-trust validation, and pre-authentication context initialization.
/// Prevents arbitrary magic fallback strings across domain, audit, and persistence layers.
/// </summary>
public static class SystemIdentityUtility
{
    public const string SystemTenantSlug = IRootAdminService.DefaultTenantSlug;

    /// <summary>
    /// Sequential / static UUID reserved for host platform background daemons, migrations, and system tasks.
    /// </summary>
    public static readonly Guid SystemDaemonTenantId = new Guid("00000000-0000-0000-0000-000000000001");

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

        if (tenantContext == null || (!tenantContext.IsHost && (!tenantContext.TenantId.HasValue || tenantContext.TenantId.Value == Guid.Empty)))
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
    /// (e.g. login gate, password verification, seeders). 
    /// Ensures 100% of subsequent operations operate under a valid, traceable daemon identity and system tenant.
    /// </summary>
    public static void EstablishPreAuthContext(
        ICurrentUser currentUser, 
        ITenantContext tenantContext, 
        string attemptedEmail, 
        string operation = "VerifyLogin")
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var normalizedEmail = string.IsNullOrWhiteSpace(attemptedEmail) ? $"preauth+{operation.ToLowerInvariant()}@system.internal" : attemptedEmail.Trim();
        currentUser.SetSystemDaemon($"PreAuth:{operation}", normalizedEmail);
        tenantContext.Initialize(
            SystemDaemonTenantId, 
            "System Host", 
            UserTypeEnum.CompanyUser, 
            Array.Empty<TenantInfo>(), 
            isHost: true,
            tenantSlug: SystemTenantSlug);
    }

    /// <summary>
    /// Dynamically constructs an internal service identifier for scoped system daemons and workers,
    /// scoped to the system tenant authority without hardcoding individual user emails.
    /// </summary>
    public static string FormatInternalDaemonEmail(string daemonName) =>
        $"system+{daemonName.ToLowerInvariant()}@{SystemTenantSlug}.internal";

    /// <summary>
    /// Determines whether a given user ID, email, or principal identifier represents an internal system daemon.
    /// Recognizes standard daemon formats (e.g. "daemon:*", "system+*@*.internal", "*.internal").
    /// </summary>
    public static bool IsSystemDaemon(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return false;
        var trimmed = identity.Trim();
        return trimmed.StartsWith("daemon:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("system+", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith($"@{SystemTenantSlug}.internal", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
    }
}
