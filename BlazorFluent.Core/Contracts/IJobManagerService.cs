using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service contract for querying job execution history, triggering on-demand executions, and retrying failed batch jobs.
/// </summary>
public interface IJobManagerService
{
    /// <summary>
    /// Fetches the most recent job execution records ordered by start timestamp.
    /// </summary>
    Task<IReadOnlyList<JobExecutionEntity>> GetRecentExecutionsAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers an on-demand batch job execution.
    /// </summary>
    Task<Result> TriggerJobAsync(string jobName, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a previously failed job execution with fresh tracking.
    /// </summary>
    Task<Result> RetryJobAsync(Guid executionId, CancellationToken cancellationToken = default);
}
