using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Models;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Jobs.Engine;

/// <summary>
/// Resilient tabular batch stager that batches in-memory records into database entities.
/// Supports pause and resumption on transient database/network errors.
/// </summary>
public abstract class TabularFileStager<TEntity, TModel>
{
    protected readonly ILogger _logger;
    protected const int DefaultBatchSize = 1000;

    protected TabularFileStager(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Stages an entire list of validated rows in discrete transactional batches.
    /// </summary>
    public async Task<Result<int>> StageBatchesAsync(
        Guid importId,
        IReadOnlyList<TModel> rows,
        int batchSize = DefaultBatchSize,
        CancellationToken ct = default)
    {
        if (rows == null || rows.Count == 0)
        {
            return Result<int>.Ok(0, "No records to stage.");
        }

        var totalStaged = 0;
        var batchNumber = 0;
        var totalBatches = (int)Math.Ceiling(rows.Count / (double)batchSize);

        try
        {
            while (totalStaged < rows.Count)
            {
                ct.ThrowIfCancellationRequested();

                var currentChunk = rows.Skip(totalStaged).Take(batchSize).ToList();
                batchNumber++;

                _logger.LogInformation("Staging import {ImportId}: processing batch {Batch}/{TotalBatches} ({Count} items)...",
                    importId, batchNumber, totalBatches, currentChunk.Count);

                var entities = currentChunk.Select(row => MapToEntity(importId, row)).ToList();
                await PersistBatchAsync(entities, ct);

                totalStaged += currentChunk.Count;
            }

            _logger.LogInformation("Successfully staged {TotalStaged} records for import {ImportId}.", totalStaged, importId);
            return Result<int>.Ok(totalStaged);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Staging was cancelled for import {ImportId} after {TotalStaged} items.", importId, totalStaged);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Staging failed on batch {Batch}/{TotalBatches} for import {ImportId} after {TotalStaged} items.",
                batchNumber, totalBatches, importId, totalStaged);

            return Result<int>.Fail($"Staging failed on batch {batchNumber}: {ex.Message}");
        }
    }

    protected abstract TEntity MapToEntity(Guid importId, TModel model);
    protected abstract Task PersistBatchAsync(IReadOnlyList<TEntity> entities, CancellationToken ct);
}
