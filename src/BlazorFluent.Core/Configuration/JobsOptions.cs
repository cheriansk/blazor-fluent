namespace BlazorFluent.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for background processing and periodic schedulers.
/// Section: "Jobs"
/// </summary>
public class JobsOptions
{
    public const string SectionName = "Jobs";

    public bool EnableHostedListener { get; set; } = true;
    public bool EnableScheduler { get; set; } = true;
    public string CronServiceAccountEmail { get; set; } = string.Empty;
    public Dictionary<string, string> Schedules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
