using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.Models;

namespace BlazorFluent.RulesEngine.BatchProcessing;

/// <summary>
/// Routes incoming staged batch payloads to the registered C# <see cref="ICloudProcessingStrategy"/> for the given FileType.
/// </summary>
public sealed class CloudProcessorDispatcher
{
    private readonly Dictionary<string, ICloudProcessingStrategy> _processorsByFileType;

    public CloudProcessorDispatcher(IEnumerable<BaseCloudProcessor> processors)
    {
        _processorsByFileType = new Dictionary<string, ICloudProcessingStrategy>(StringComparer.OrdinalIgnoreCase);
        foreach (var processor in processors)
        {
            foreach (var fileType in processor.SupportedFileTypes)
            {
                _processorsByFileType[fileType] = processor;
            }
        }
    }

    public ProcessBatchRespDto Dispatch(
        Guid importId,
        Guid importFileId,
        int chunkIndex,
        string fileType,
        CanonicalStagedChunkPayload payload,
        IReadOnlyDictionary<string, string>? contextParameters)
    {
        if (string.IsNullOrWhiteSpace(fileType) ||
            !_processorsByFileType.TryGetValue(fileType, out var strategy))
        {
            return new ProcessBatchRespDto(
                ImportFileId: importFileId,
                ChunkIndex: chunkIndex,
                IsSuccess: false,
                IsEncryptedAtRest: false,
                ProcessedRows: [],
                Errors:
                [
                    new FileValidationError(
                        SheetName: payload.SheetName ?? "Default",
                        RowIndex: 0,
                        ColumnName: "FileType",
                        ErrorMessage: $"No cloud processing strategy is registered for fileType '{fileType}'.")
                ],
                ErrorMessage: $"No cloud processing strategy registered for fileType '{fileType}'.");
        }

        return strategy.ProcessChunk(
            importId,
            importFileId,
            chunkIndex,
            fileType,
            payload,
            contextParameters);
    }
}
