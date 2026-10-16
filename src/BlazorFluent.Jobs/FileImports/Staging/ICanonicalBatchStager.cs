using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Shared.Contracts.Engine.Transforms;

namespace BlazorFluent.Jobs.FileImports.Staging;

/// <summary>
/// Contract for pre-ingestion batch stagers that validate incoming streams locally
/// and stage normalized high-density JSON chunks into 'staging.StagedImports'.
/// </summary>
public interface ICanonicalBatchStager
{
    /// <summary>
    /// Strategy identifier handled by this stager (e.g., "generic-json" or "custom:VendorInvoiceXmlBatchStager").
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Streams, validates (fail-closed / all-or-nothing), and stages the file into 'staging.StagedImports' in chunks.
    /// Returns the total number of valid staged rows across all chunks.
    /// </summary>
    Task<Result<int>> ValidateAndStageCanonicalJsonAsync(
        ImportFileEntity file,
        Stream decryptedStream,
        FileTemplateConfig config,
        CancellationToken ct = default);
}
