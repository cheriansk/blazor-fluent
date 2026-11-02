namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for background processing, periodic schedulers, and transactional outbox.
/// Section: "Jobs"
/// </summary>
public class JobsOptions
{
    public const string SectionName = "Jobs";

    public bool EnableHostedListener { get; set; } = true;
    public bool EnableScheduler { get; set; } = true;
    public bool EnableOutboxDispatcher { get; set; } = true;
    public int OutboxPollingIntervalSeconds { get; set; } = 10;
    public int OutboxBatchSize { get; set; } = 50;
    public string CronServiceAccountEmail { get; set; } = string.Empty;
    public Dictionary<string, string> Schedules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
