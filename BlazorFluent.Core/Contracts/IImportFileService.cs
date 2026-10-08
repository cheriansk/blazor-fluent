using BlazorFluent.Core.Dtos.Imports;
using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Domain service contract managing encrypted file uploads, asynchronous batch event scheduling,
/// and forensic audit inspections for the file import and processing subsystem.
/// </summary>
public interface IImportFileService
{
    /// <summary>
    /// Encrypts raw file streams via AES-256-GCM, uploads ciphertext into a dedicated batch folder
    /// in Azure Blob Storage, persists an ImportFileEntity metadata record per file with a shared ImportId,
    /// dispatches a FileImportBatchJobEvent into the background queue, and issues an in-app confirmation notification.
    /// Strictly atomic: if any file fails upload, the entire batch is aborted and cleaned up.
    /// </summary>
    Task<Result<Guid>> UploadBatchAndQueueAsync(UploadBatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Legacy single-file helper that forwards to UploadBatchAndQueueAsync as a batch of size 1.
    /// </summary>
    Task<Result<Guid>> UploadAndQueueAsync(UploadFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves recent import file records for the active project workspace.
    /// </summary>
    Task<IReadOnlyList<ImportFileSummaryDto>> GetImportHistoryAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all files belonging to a specific batch ImportId within a project workspace.
    /// </summary>
    Task<IReadOnlyList<ImportFileSummaryDto>> GetImportFilesByBatchAsync(Guid projectId, Guid importId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves complete file details, blob storage references, and parsed validation error diagnostics within a project workspace.
    /// </summary>
    Task<ImportFileDetailsDto?> GetImportFileDetailsAsync(Guid projectId, Guid importFileId, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes an import file record and removes associated staging records within a project workspace.
    /// </summary>
    Task<Result<bool>> DeleteImportFileAsync(Guid projectId, Guid importFileId, CancellationToken ct = default);
}
