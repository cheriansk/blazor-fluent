using System;
using System.Collections.Generic;
using System.Linq;
using BlazorEnterprise.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Infrastructure.Security;

/// <summary>
/// Singleton service providing access to root super-administrators and root tenant parameters parsed from configuration/KeyVault.
/// Enforces a strict limit of 3 administrators and zero hardcoded fallbacks (Fail-Closed).
/// </summary>
public sealed class RootAdminService : IRootAdminService
{
    private readonly IReadOnlyList<string> _adminEmails;
    private readonly ILogger<RootAdminService> _logger;

    public Guid RootTenantId { get; }
    public string RootTenantSlug { get; }
    public string RootTenantName { get; }

    public RootAdminService(IConfiguration configuration, ILogger<RootAdminService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(configuration);

        // Resolve Root Tenant metadata strictly from Configuration / KeyVault
        var rawTenantId = configuration["Admin:RootTenantId"];
        if (string.IsNullOrWhiteSpace(rawTenantId) || !Guid.TryParse(rawTenantId, out var parsedId) || parsedId == Guid.Empty)
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Admin:RootTenantId' is required and must be a valid, non-empty GUID (Fail-Closed).");
        }
        RootTenantId = parsedId;

        var rawSlug = configuration["Admin:RootTenantSlug"];
        if (string.IsNullOrWhiteSpace(rawSlug))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Admin:RootTenantSlug' is required and cannot be empty (Fail-Closed).");
        }
        RootTenantSlug = rawSlug.Trim().ToLowerInvariant();

        var rawName = configuration["Admin:RootTenantName"];
        if (string.IsNullOrWhiteSpace(rawName))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Admin:RootTenantName' is required and cannot be empty (Fail-Closed).");
        }
        RootTenantName = rawName.Trim();

        var rawAdmins = configuration["Admin:RootAdmins"];
        if (string.IsNullOrWhiteSpace(rawAdmins))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Admin:RootAdmins' is required and must contain at least one administrator email address (Fail-Closed).");
        }

        var parsed = rawAdmins
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parsed.Count == 0)
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Admin:RootAdmins' must contain at least one valid administrator email (Fail-Closed).");
        }

        if (parsed.Count > IRootAdminService.MaxRootAdminCount)
        {
            _logger.LogWarning(
                "SECURITY ALERT: Configuration 'Admin:RootAdmins' specified {Count} users, but the system allows a maximum of {MaxCount} root administrators. Truncating to the first {MaxCount}.",
                parsed.Count, IRootAdminService.MaxRootAdminCount, IRootAdminService.MaxRootAdminCount);
            _adminEmails = parsed.Take(IRootAdminService.MaxRootAdminCount).ToList();
        }
        else
        {
            _adminEmails = parsed;
        }

        _logger.LogInformation("RootAdminService initialized with RootTenantId={RootTenantId}, RootTenantSlug='{RootTenantSlug}', {Count} root administrator(s): {Admins}",
            RootTenantId, RootTenantSlug, _adminEmails.Count, string.Join(", ", _adminEmails));
    }

    public IReadOnlyList<string> GetRootAdminEmails() => _adminEmails;

    public bool IsRootAdmin(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();
        return _adminEmails.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }
}
