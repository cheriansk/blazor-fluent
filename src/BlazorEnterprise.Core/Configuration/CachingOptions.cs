namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for L1 in-memory and L2 distributed caching.
/// Section: "Caching"
/// </summary>
public class CachingOptions
{
    public const string SectionName = "Caching";

    public int DefaultExpirationMinutes { get; set; } = 60;
    public int LocalCacheExpirationMinutes { get; set; } = 15;
}
