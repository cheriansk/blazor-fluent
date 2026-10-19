using BlazorFluent.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Originating trigger source that initiated a background batch job execution.
/// Follows the *Enum naming convention.
/// </summary>
public enum JobTriggerSourceEnum
{
    [Display(Name = "Cron", Description = "Scheduled periodic timer or Cronos expression")]
    [DataListCategory(JobTriggerSourceDefinitions.Categories.Scheduled)]
    Cron = 1,

    [Display(Name = "Manual", Description = "Manually triggered by an operator from the UI or API")]
    [DataListCategory(JobTriggerSourceDefinitions.Categories.Interactive)]
    Manual = 2,

    [Display(Name = "Domain Event", Description = "Dispatched by an in-process domain event")]
    [DataListCategory(JobTriggerSourceDefinitions.Categories.Automated)]
    Event = 3,

    [Display(Name = "File Import", Description = "Triggered by bulk spreadsheet or file upload ingestion")]
    [DataListCategory(JobTriggerSourceDefinitions.Categories.Automated)]
    FileImport = 4,

    [Display(Name = "Retry", Description = "Automated retry backoff or operator retry")]
    [DataListCategory(JobTriggerSourceDefinitions.Categories.Interactive)]
    Retry = 5,

    [Display(Name = "Webhook", Description = "Inbound HTTP webhook or external system trigger")]
    [DataListCategory(JobTriggerSourceDefinitions.Categories.Automated)]
    Webhook = 6
}

public static class JobTriggerSourceDefinitions
{
    public static class Categories
    {
        [Display(Name = "Scheduled", Description = "Time-based scheduled execution")]
        public const string Scheduled = "Scheduled";

        [Display(Name = "Interactive", Description = "User or operator initiated execution")]
        public const string Interactive = "Interactive";

        [Display(Name = "Automated", Description = "System or event-driven execution")]
        public const string Automated = "Automated";
    }
}

public static class JobTriggerSourceExtensions
{
    /// <summary>
    /// Safely maps raw trigger string tags (e.g. 'Button:RunCatalogSync', 'Cron', 'FileImport:xlsx') to the strongly-typed enum.
    /// </summary>
    public static JobTriggerSourceEnum ParseTriggerSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return JobTriggerSourceEnum.Cron;

        if (source.StartsWith("Button:", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("Manual", StringComparison.OrdinalIgnoreCase))
        {
            return JobTriggerSourceEnum.Manual;
        }

        if (source.StartsWith("Cron", StringComparison.OrdinalIgnoreCase))
        {
            return JobTriggerSourceEnum.Cron;
        }

        if (source.StartsWith("FileImport", StringComparison.OrdinalIgnoreCase))
        {
            return JobTriggerSourceEnum.FileImport;
        }

        if (source.StartsWith("Retry", StringComparison.OrdinalIgnoreCase))
        {
            return JobTriggerSourceEnum.Retry;
        }

        if (source.StartsWith("Webhook", StringComparison.OrdinalIgnoreCase))
        {
            return JobTriggerSourceEnum.Webhook;
        }

        if (Enum.TryParse<JobTriggerSourceEnum>(source, true, out var parsed))
        {
            return parsed;
        }

        return JobTriggerSourceEnum.Event;
    }
}
