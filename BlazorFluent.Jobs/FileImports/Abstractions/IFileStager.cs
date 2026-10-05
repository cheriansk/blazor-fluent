using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Imports;

namespace BlazorFluent.Jobs.FileImports.Abstractions;

/// <summary>
/// Contract for validating incoming decrypted file payloads and persisting records to staging tables.
/// Auto-discovered by the generic batch runner via DI assembly scanning.
/// </summary>
public interface IFileStager
{
    /// <summary>
    /// Indicates whether this stager can handle the specified file format/target type.
    /// </summary>
    bool CanHandle(ImportFileType fileType);

    /// <summary>
    /// Parses, strictly validates (fail-closed), and persists incoming data into the feature staging table.
    /// Updates ImportFileEntity metrics, status, and diagnostic errors.
    /// </summary>
    Task<Result<int>> ValidateAndStageAsync(ImportFileEntity file, Stream decryptedStream, CancellationToken ct = default);
}
