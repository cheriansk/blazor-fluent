using System;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Domain contract providing centralized system daemon identity generation, validation,
/// and actor resolution dynamically derived from configuration without hardcoded magic literals.
/// </summary>
public interface ISystemIdentity
{
    /// <summary>Unique identifier for the platform Root Tenant.</summary>
    Guid RootTenantId { get; }

    /// <summary>Vanity URL slug for the platform Root Tenant.</summary>
    string RootTenantSlug { get; }

    /// <summary>Display name for the platform Root Tenant.</summary>
    string RootTenantName { get; }

    /// <summary>Auditable name for system-originated background operations.</summary>
    string SystemActorName { get; }

    /// <summary>Configured local-part for tenant daemons (e.g. "daemon").</summary>
    string SystemDaemonLocalPart { get; }

    /// <summary>Configured domain for tenant daemons (e.g. "internal.local").</summary>
    string SystemDaemonEmailDomain { get; }

    /// <summary>
    /// Constructs a fully-qualified daemon email address for the specified tenant slug.
    /// </summary>
    string DaemonEmailFor(string tenantSlug);

    /// <summary>
    /// Constructs a system service worker email scoped to the root tenant authority.
    /// </summary>
    string FormatInternalDaemonEmail(string daemonName);

    /// <summary>
    /// Evaluates whether a given email, identity, or actor represents an authorized internal system daemon.
    /// </summary>
    bool IsSystemDaemon(string? identity);
}
