using System;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Utilities;

/// <summary>
/// Central authority for system identity semantics, zero-trust validation, and pre-authentication context initialization.
/// Prevents arbitrary magic fallback strings across domain, audit, and persistence layers.
/// </summary>
public static class SystemIdentityUtility
{
    public const string SystemTenantSlug = IRootAdminService.DefaultTenantSlug;

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

        if (tenantContext == null || string.IsNullOrWhiteSpace(tenantContext.TenantId))
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

        var normalizedEmail = string.IsNullOrWhiteSpace(attemptedEmail) ? "anonymous@preauth.local" : attemptedEmail.Trim();
        currentUser.SetSystemDaemon($"PreAuth:{operation}", normalizedEmail);
        tenantContext.Initialize(
            SystemTenantSlug, 
            "System Host", 
            UserType.CompanyUser, 
            Array.Empty<TenantInfo>(), 
            isHost: true);
    }
}
