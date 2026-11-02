using Microsoft.Extensions.Options;
using BlazorEnterprise.Core.Configuration;
namespace BlazorEnterprise.Api.Admin;

public interface IAdminSecurityService
{
    bool IsAuthorizedAdmin(string? email);
    IReadOnlyList<string> GetConfiguredAdmins();
    string GetAllowedDomain();
}

public class AdminSecurityService : IAdminSecurityService
{
    private readonly IConfiguration? _configuration;
    private readonly IOptions<AdminOptions>? _adminOptions;
    private readonly ILogger<AdminSecurityService> _logger;

    public AdminSecurityService(
        IConfiguration configuration,
        ILogger<AdminSecurityService> logger)
        : this(logger, null, configuration)
    {
    }

    public AdminSecurityService(
        ILogger<AdminSecurityService> logger,
        IOptions<AdminOptions>? adminOptions = null,
        IConfiguration? configuration = null)
    {
        _logger = logger;
        _adminOptions = adminOptions;
        _configuration = configuration;
    }

    public string GetAllowedDomain() =>
        (!string.IsNullOrWhiteSpace(_adminOptions?.Value.AllowedDomain) ? _adminOptions.Value.AllowedDomain : _configuration?["Admin:AllowedDomain"])
        ?? throw new InvalidOperationException("Zero-Trust Security Violation: 'Admin:AllowedDomain' must be explicitly configured.");

    public IReadOnlyList<string> GetConfiguredAdmins()
    {
        // Loaded from Azure Key Vault in production or appsettings in development
        var rawAdmins = (!string.IsNullOrWhiteSpace(_adminOptions?.Value.KeyVaultUsers) ? _adminOptions.Value.KeyVaultUsers : (!string.IsNullOrWhiteSpace(_adminOptions?.Value.AuthorizedEmails) ? _adminOptions.Value.AuthorizedEmails : _configuration?["Admin:KeyVaultUsers"]))
            ?? _configuration["Admin:AuthorizedEmails"]
            ?? throw new InvalidOperationException("Zero-Trust Security Violation: 'Admin:KeyVaultUsers' must be explicitly configured.");

        return rawAdmins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(3) // Hard ceiling of at most 3 admin users
            .ToList();
    }

    public bool IsAuthorizedAdmin(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var cleanEmail = email.Trim().ToLowerInvariant();
        var domain = GetAllowedDomain().ToLowerInvariant().TrimStart('@');

        // Check 1: Domain matching
        if (!cleanEmail.EndsWith($"@{domain}"))
        {
            _logger.LogWarning("Admin access denied for {Email}: domain does not match allowed domain {Domain}", email, domain);
            return false;
        }

        // Check 2: Max 3 Azure Key Vault configured identities
        var configuredAdmins = GetConfiguredAdmins()
            .Select(a => a.ToLowerInvariant())
            .ToHashSet();

        var isMatch = configuredAdmins.Contains(cleanEmail);
        if (!isMatch)
        {
            _logger.LogWarning("Admin access denied for {Email}: user is not one of the authorized Key Vault admin users", email);
        }

        return isMatch;
    }
}
