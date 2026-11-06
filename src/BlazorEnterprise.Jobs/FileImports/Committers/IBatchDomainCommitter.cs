using BlazorEnterprise.Core.Domain.Imports;
using BlazorEnterprise.Core.Events.Imports;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;

namespace BlazorEnterprise.Jobs.FileImports.Committers;

/// <summary>
/// Strategy interface for committing Cloud API-processed domain rows into database entities.
/// Eliminates hardcoded entity-specific commit blocks from GenericFileImportBatchJobHandler.
/// </summary>
public interface IBatchDomainCommitter
{
    /// <summary>
    /// Determines whether this committer handles rows destined for the specified target entity.
    /// </summary>
    bool CanCommit(string targetEntityName);

    /// <summary>
    /// Atomically commits processed domain rows to PostgreSQL within a dedicated unit of work.
    /// Returns the number of entities successfully committed.
    /// </summary>
    Task<Result<int>> CommitProcessedRowsAsync(
        ImportFileEntity file,
        IReadOnlyList<ProcessedDomainRowDto> rows,
        FileImportBatchJobEvent jobEvent,
        CancellationToken ct = default);
}
