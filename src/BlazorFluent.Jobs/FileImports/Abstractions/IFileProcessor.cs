using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Jobs.FileImports.Abstractions;

/// <summary>
/// Contract for promoting staged records from staging tables into live domain entities.
/// Auto-discovered by the generic batch runner via DI assembly scanning.
/// </summary>
public interface IFileProcessor
{
    /// <summary>
    /// Indicates whether this processor can handle the specified file format/target type.
    /// </summary>
    bool CanHandle(ImportFileType fileType);

    /// <summary>
    /// Promotes staged records belonging to the specified import file into live domain entities.
    /// Updates ImportFileEntity status and stage upon completion.
    /// </summary>
    Task<Result<int>> ProcessAsync(ImportFileEntity file, CancellationToken ct = default);
}
