using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Status of a background batch job execution.
/// </summary>
public enum JobStatus
{
    [Display(Name = "Queued", Description = "Job is queued in channel awaiting worker pickup")]
    [DataListCategory(JobStatusDefinitions.Categories.Pending)]
    Queued = 1,

    [Display(Name = "Running", Description = "Job is actively executing")]
    [DataListCategory(JobStatusDefinitions.Categories.Active)]
    Running = 2,

    [Display(Name = "Succeeded", Description = "Job completed successfully without errors")]
    [DataListCategory(JobStatusDefinitions.Categories.Completed)]
    Succeeded = 3,

    [Display(Name = "Failed", Description = "Job failed after exhausting retries")]
    [DataListCategory(JobStatusDefinitions.Categories.Completed)]
    Failed = 4,

    [Display(Name = "Retrying", Description = "Job failed attempt and is awaiting retry backoff")]
    [DataListCategory(JobStatusDefinitions.Categories.Active)]
    Retrying = 5
}

public static class JobStatusDefinitions
{
    public static class Categories
    {
        [Display(Name = "Pending Execution", Description = "Jobs awaiting execution")]
        public const string Pending = "Pending";

        [Display(Name = "In Progress", Description = "Jobs actively running or retrying")]
        public const string Active = "Active";

        [Display(Name = "Terminal States", Description = "Final execution outcomes")]
        public const string Completed = "Completed";
    }

    public static class Filters
    {
        [Display(Name = "Active Jobs", Description = "Filters for active and pending jobs")]
        public const string ActiveOnly = "ActiveOnly";
    }
}
