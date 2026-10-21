using BlazorFluent.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Status of a background batch job execution.
/// </summary>
[LifecycleEnum]
public enum JobStatusEnum
{
    [Display(Name = "Queued", Description = "Job is queued in channel awaiting worker pickup")]
    [DataListCategory(JobStatusEnumDefinitions.Categories.Pending)]
    Queued = 1,

    [Display(Name = "Running", Description = "Job is actively executing")]
    [DataListCategory(JobStatusEnumDefinitions.Categories.Active)]
    Running = 2,

    [Display(Name = "Succeeded", Description = "Job completed successfully without errors")]
    [DataListCategory(JobStatusEnumDefinitions.Categories.Completed)]
    Succeeded = 3,

    [Display(Name = "Failed", Description = "Job failed after exhausting retries")]
    [DataListCategory(JobStatusEnumDefinitions.Categories.Completed)]
    Failed = 4,

    [Display(Name = "Retrying", Description = "Job failed attempt and is awaiting retry backoff")]
    [DataListCategory(JobStatusEnumDefinitions.Categories.Active)]
    Retrying = 5
}

public static class JobStatusEnumDefinitions
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

    public static readonly IReadOnlyDictionary<JobStatusEnum, IReadOnlySet<JobStatusEnum>> AllowedTransitions =
        new Dictionary<JobStatusEnum, IReadOnlySet<JobStatusEnum>>
        {
            [JobStatusEnum.Queued] = new HashSet<JobStatusEnum> { JobStatusEnum.Running, JobStatusEnum.Failed },
            [JobStatusEnum.Running] = new HashSet<JobStatusEnum> { JobStatusEnum.Succeeded, JobStatusEnum.Failed, JobStatusEnum.Retrying },
            [JobStatusEnum.Retrying] = new HashSet<JobStatusEnum> { JobStatusEnum.Running, JobStatusEnum.Failed },
            [JobStatusEnum.Failed] = new HashSet<JobStatusEnum> { JobStatusEnum.Queued },
            [JobStatusEnum.Succeeded] = new HashSet<JobStatusEnum>()
        };
    }
}
