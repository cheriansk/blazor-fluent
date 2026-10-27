using BlazorEnterprise.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorEnterprise.Shared.DataListTypes;

/// <summary>
/// Originating trigger source that initiated a background batch job execution.
/// Follows the *Enum naming convention.
/// </summary>
[ClassificationEnum]
public enum JobTriggerSourceEnum
{
    [Display(Name = "Cron", Description = "Scheduled periodic timer or Cronos expression")]
    [DataListCategory(JobTriggerSourceEnumDefinitions.Categories.Scheduled)]
    Cron = 1,

    [Display(Name = "Manual", Description = "Manually triggered by an operator from the UI or API")]
    [DataListCategory(JobTriggerSourceEnumDefinitions.Categories.Interactive)]
    Manual = 2,

    [Display(Name = "Domain Event", Description = "Dispatched by an in-process domain event")]
    [DataListCategory(JobTriggerSourceEnumDefinitions.Categories.Automated)]
    Event = 3,

    [Display(Name = "File Import", Description = "Triggered by bulk spreadsheet or file upload ingestion")]
    [DataListCategory(JobTriggerSourceEnumDefinitions.Categories.Automated)]
    FileImport = 4,

    [Display(Name = "Retry", Description = "Automated retry backoff or operator retry")]
    [DataListCategory(JobTriggerSourceEnumDefinitions.Categories.Interactive)]
    Retry = 5,

    [Display(Name = "Webhook", Description = "Inbound HTTP webhook or external system trigger")]
    [DataListCategory(JobTriggerSourceEnumDefinitions.Categories.Automated)]
    Webhook = 6
}

public static class JobTriggerSourceEnumDefinitions
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
    /// Safely maps raw trigger string tags (e.g. 'Button:RunTaskCadenceAlert', 'Cron', 'FileImport:xlsx') to the strongly-typed enum.
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

        if (source.TryToEnum<JobTriggerSourceEnum>(out var parsed))
        {
            return parsed;
        }

        return JobTriggerSourceEnum.Event;
    }
}
