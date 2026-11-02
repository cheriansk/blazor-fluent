namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for JWT token generation and authentication gateway.
/// Section: "Auth"
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
