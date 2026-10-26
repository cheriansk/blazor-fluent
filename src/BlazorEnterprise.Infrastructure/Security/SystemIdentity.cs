using System;
using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Core.Contracts;
using Microsoft.Extensions.Options;

namespace BlazorEnterprise.Infrastructure.Security;

/// <summary>
/// Infrastructure service providing dynamic system identity resolution,
/// daemon email generation, and zero-trust actor validation derived strictly from configuration.
/// </summary>
public class SystemIdentity : ISystemIdentity
{
    private readonly IRootAdminService _rootAdminService;
    private readonly SystemIdentityOptions _options;

    public SystemIdentity(IRootAdminService rootAdminService, IOptions<SystemIdentityOptions> options)
    {
        _rootAdminService = rootAdminService ?? throw new ArgumentNullException(nameof(rootAdminService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.SystemDaemonLocalPart))
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Identity:SystemDaemonLocalPart' is required and cannot be empty (Fail-Closed).");

        if (string.IsNullOrWhiteSpace(_options.SystemDaemonEmailDomain))
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Identity:SystemDaemonEmailDomain' is required and cannot be empty (Fail-Closed).");

        if (string.IsNullOrWhiteSpace(_options.SystemActorName))
            throw new InvalidOperationException("Zero-Trust Security Violation: Configuration 'Identity:SystemActorName' is required and cannot be empty (Fail-Closed).");
    }

    public Guid RootTenantId => _rootAdminService.RootTenantId;
    public string RootTenantSlug => _rootAdminService.RootTenantSlug;
    public string RootTenantName => _rootAdminService.RootTenantName;
    public string SystemActorName => _options.SystemActorName;
    public string SystemDaemonLocalPart => _options.SystemDaemonLocalPart;
    public string SystemDaemonEmailDomain => _options.SystemDaemonEmailDomain;

    public string DaemonEmailFor(string tenantSlug)
    {
        var slug = string.IsNullOrWhiteSpace(tenantSlug) 
            ? RootTenantSlug 
            : tenantSlug.Trim().ToLowerInvariant();

        return $"{_options.SystemDaemonLocalPart}@{slug}.{_options.SystemDaemonEmailDomain}";
    }

    public string FormatInternalDaemonEmail(string daemonName)
    {
        var name = string.IsNullOrWhiteSpace(daemonName) 
            ? "service" 
            : daemonName.Trim().ToLowerInvariant();

        return $"{name}@{RootTenantSlug}.{_options.SystemDaemonEmailDomain}";
    }

    public bool IsSystemDaemon(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return false;

        var trimmed = identity.Trim();

        return trimmed.StartsWith("daemon:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith($"{_options.SystemDaemonLocalPart}@", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("system+", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith($".{_options.SystemDaemonEmailDomain}", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(_options.SystemActorName, StringComparison.OrdinalIgnoreCase);
    }
}
