using BlazorFluent.Shared.Contracts.Engine.Transforms;

namespace BlazorFluent.RulesEngine.BatchProcessing;

/// <summary>
/// Stateless cloud processing strategy for a specific file type.
/// Enforces all-or-nothing atomicity on the staged chunk payload.
/// </summary>
public interface ICloudProcessingStrategy
{
    /// <summary>
    /// Evaluates and transforms all rows in the staged chunk payload.
    /// If any row fails validation or domain business rules, the entire batch fails with zero processed rows.
    /// </summary>
    ProcessBatchRespDto ProcessChunk(
        Guid importId,
        Guid importFileId,
        int chunkIndex,
        string fileType,
        CanonicalStagedChunkPayload payload,
        IReadOnlyDictionary<string, string>? contextParameters);
}

/// <summary>
/// Base class for strictly typed C# cloud processors with built-in dynamic alias (headerMap) resolution.
/// </summary>
public abstract class BaseCloudProcessor : ICloudProcessingStrategy
{
    /// <summary>
    /// Supported file types for this processor (case-insensitive).
    /// </summary>
    public abstract IReadOnlyCollection<string> SupportedFileTypes { get; }

    public abstract ProcessBatchRespDto ProcessChunk(
        Guid importId,
        Guid importFileId,
        int chunkIndex,
        string fileType,
        CanonicalStagedChunkPayload payload,
        IReadOnlyDictionary<string, string>? contextParameters);

    /// <summary>
    /// Builds a reverse lookup from canonical property name (case-insensitive) to compact alias ("c1", "c2", etc.).
    /// </summary>
    protected static Dictionary<string, string> BuildReverseHeaderMap(IReadOnlyDictionary<string, string> headerMap)
    {
        var reverseMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in headerMap)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                reverseMap[kvp.Value] = kvp.Key;
            }
        }
        return reverseMap;
    }

    /// <summary>
    /// Resolves a cell value from a staged row using the canonical property name or direct alias fallback.
    /// </summary>
    protected static string? GetCellValue(
        StagedRowDto row,
        IReadOnlyDictionary<string, string> reverseHeaderMap,
        string canonicalPropertyName)
    {
        if (reverseHeaderMap.TryGetValue(canonicalPropertyName, out var alias) &&
            row.D.TryGetValue(alias, out var value))
        {
            return value;
        }

        if (row.D.TryGetValue(canonicalPropertyName, out var directValue))
        {
            return directValue;
        }

        return null;
    }
}
