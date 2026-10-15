using System.Text.RegularExpressions;

namespace BlazorFluent.Shared.Common;

/// <summary>
/// Defensive sanitizer for distributed Correlation IDs to block CRLF and header injection attacks.
/// </summary>
public static partial class CorrelationIdSanitizer
{
    private static readonly Regex SafeCorrelationRegex = new(@"^[a-zA-Z0-9\-_]{1,64}$", RegexOptions.Compiled);

    /// <summary>
    /// Validates whether a correlation ID is structurally sound.
    /// Returns true if it consists only of alphanumeric, hyphen, or underscore characters and is 1-64 characters long.
    /// </summary>
    public static bool IsValid(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return false;
        return SafeCorrelationRegex.IsMatch(correlationId.Trim());
    }

    /// <summary>
    /// Sanitizes an input correlation ID, returning a cleaned version or null if invalid.
    /// </summary>
    public static string? Sanitize(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return null;
        var trimmed = correlationId.Trim();
        return SafeCorrelationRegex.IsMatch(trimmed) ? trimmed : null;
    }
}
