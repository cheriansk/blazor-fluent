using System;
using System.Collections.Generic;
using System.Linq;
using BlazorFluent.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Singleton service providing access to root super-administrators and root tenant parameters parsed from configuration/KeyVault.
/// Enforces a strict limit of 3 administrators.
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
        _logger = logger;

        // Resolve Root Tenant metadata from Configuration / KeyVault
        var rawTenantId = configuration["Admin:RootTenantId"] ?? configuration["Security:RootTenantId"];
        if (!string.IsNullOrWhiteSpace(rawTenantId) && Guid.TryParse(rawTenantId, out var parsedId))
        {
            RootTenantId = parsedId;
        }
        else
        {
            RootTenantId = new Guid("00000000-0000-0000-0000-000000000001");
        }

        RootTenantSlug = configuration["Admin:RootTenantSlug"]
            ?? configuration["Security:RootTenantSlug"]
            ?? "root";

        RootTenantName = configuration["Admin:RootTenantName"]
            ?? configuration["Security:RootTenantName"]
            ?? "System Administration";

        var rawConfig = configuration["Admin:RootAdmins"] ?? configuration["Security:RootAdmins"] ?? configuration["Admin:KeyVaultUsers"] ?? string.Empty;
        var parsed = rawConfig
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parsed.Count > IRootAdminService.MaxRootAdminCount)
        {
            _logger.LogWarning(
                "SECURITY ALERT: Configuration 'Security:RootAdmins' specified {Count} users, but the system allows a maximum of {MaxCount} root administrators. Truncating to the first {MaxCount}.",
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
