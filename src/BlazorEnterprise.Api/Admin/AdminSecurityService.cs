using BlazorEnterprise.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorEnterprise.Api.Admin;

public interface IAdminSecurityService
{
    string GetAllowedDomain();
    IReadOnlyList<string> GetConfiguredAdmins();
    bool IsAllowedOrigin(string origin);
    bool IsAuthorizedAdmin(string? email);
}

public class AdminSecurityService : IAdminSecurityService
{
    private readonly IOptions<AdminOptions> _adminOptions;
    private readonly ILogger<AdminSecurityService> _logger;



    public AdminSecurityService(
        ILogger<AdminSecurityService> logger,
        IOptions<AdminOptions> adminOptions)
    {
        _logger = logger;
        _adminOptions = adminOptions;
    }

    public string GetAllowedDomain() =>
        (!string.IsNullOrWhiteSpace(_adminOptions.Value.AllowedDomain) ? _adminOptions.Value.AllowedDomain : null)
        ?? throw new InvalidOperationException("Zero-Trust Security Violation: 'Admin:AllowedDomain' must be explicitly configured.");

    public IReadOnlyList<string> GetConfiguredAdmins()
    {
        var rawAdmins = (!string.IsNullOrWhiteSpace(_adminOptions.Value.KeyVaultUsers) ? _adminOptions.Value.KeyVaultUsers : _adminOptions.Value.AuthorizedEmails)
            ?? throw new InvalidOperationException("Zero-Trust Security Violation: 'Admin:KeyVaultUsers' must be explicitly configured.");

        return rawAdmins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(3)
            .ToList();
    }

    public bool IsAuthorizedAdmin(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var allowedDomain = GetAllowedDomain();
        if (!email.EndsWith($"@{allowedDomain}", StringComparison.OrdinalIgnoreCase)) return false;
        var configured = GetConfiguredAdmins();
        return configured.Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowedOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        return origin.Equals("https://internal.antigravity.company", StringComparison.OrdinalIgnoreCase)
            || origin.Equals("https://admin.antigravity.company", StringComparison.OrdinalIgnoreCase);
    }
}