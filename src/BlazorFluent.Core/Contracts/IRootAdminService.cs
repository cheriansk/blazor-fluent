using System;
using System.Collections.Generic;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service managing the system's root super-administrators and platform root tenant.
/// Loaded dynamically from configuration/KeyVault with a strict maximum of 3 administrators.
/// </summary>
public interface IRootAdminService
{
    /// <summary>
    /// Gets the list of authorized root super-administrator emails (max 3, normalized to lowercase).
    /// </summary>
    IReadOnlyList<string> GetRootAdminEmails();

    /// <summary>
    /// Determines whether the specified email belongs to a designated root super-administrator.
    /// </summary>
    bool IsRootAdmin(string? email);

    /// <summary>
    /// Unique identifier for the platform Root Tenant. Configured via Admin:RootTenantId or KeyVault.
    /// </summary>
    Guid RootTenantId { get; }

    /// <summary>
    /// Slug for the platform Root Tenant. Configured via Admin:RootTenantSlug or KeyVault.
    /// </summary>
    string RootTenantSlug { get; }

    /// <summary>
    /// Display name for the platform Root Tenant. Configured via Admin:RootTenantName or KeyVault.
    /// </summary>
    string RootTenantName { get; }

    /// <summary>
    /// Maximum allowed root administrators in the system.
    /// </summary>
    public const int MaxRootAdminCount = 3;
}
