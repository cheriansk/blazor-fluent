using BlazorFluent.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Singleton service providing access to root super-administrators parsed from configuration.
/// Enforces a strict limit of 3 administrators.
/// </summary>
public sealed class RootAdminService : IRootAdminService
{
    private readonly IReadOnlyList<string> _adminEmails;
    private readonly ILogger<RootAdminService> _logger;

    public RootAdminService(IConfiguration configuration, ILogger<RootAdminService> logger)
    {
        _logger = logger;

        var rawConfig = configuration["Security:RootAdmins"] ?? string.Empty;
        var parsed = rawConfig
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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

        _logger.LogInformation("RootAdminService initialized with {Count} root administrator(s): {Admins}",
            _adminEmails.Count, string.Join(", ", _adminEmails));
    }

    public IReadOnlyList<string> GetRootAdminEmails() => _adminEmails;

    public bool IsRootAdmin(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();
        return _adminEmails.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }
}
