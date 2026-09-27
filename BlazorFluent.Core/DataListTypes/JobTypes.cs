namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Status of a background batch job execution.
/// </summary>
public enum JobStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Retrying = 5
}
