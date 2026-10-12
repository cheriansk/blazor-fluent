namespace BlazorFluent.Core.Storage;

/// <summary>
/// Provides secure multi-tenant blob storage abstraction for file import payloads.
/// Operates hierarchically: tenants/{tenantId}/projects/{projectId}/imports/{year}/{month}/{fileId}.bin
/// Seamlessly targets Azure Blob Storage in production or encrypted local storage in offline dev.
/// </summary>
public interface ITenantBlobStorageService
{
    /// <summary>
    /// Uploads an encrypted stream to the specified blob path.
    /// Returns the canonical blob URI or relative storage identifier.
    /// </summary>
    Task<string> UploadBlobAsync(string blobPath, Stream contentStream, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an encrypted stream from the specified blob path or URI.
    /// </summary>
    Task<Stream> DownloadBlobAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob from storage if it exists.
    /// </summary>
    Task<bool> DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the standardized hierarchical storage path:
    /// tenants/{tenantId}/projects/{projectId}/imports/{yyyy}/{MM}/{importFileId}.bin
    /// </summary>
    string BuildStoragePath(string tenantId, Guid projectId, Guid importFileId, string extension = ".bin");

    /// <summary>
    /// Builds the directory path for a batch containing multiple files:
    /// tenants/{tenantId}/projects/{projectId}/imports/{yyyy}/{MM}/batch-{importId}/
    /// </summary>
    string BuildBatchFolderPath(string tenantId, Guid projectId, Guid importId);

    /// <summary>
    /// Builds the item storage path inside a batch folder:
    /// tenants/{tenantId}/projects/{projectId}/imports/{yyyy}/{MM}/batch-{importId}/{fileId}{extension}
    /// </summary>
    string BuildBatchItemStoragePath(string tenantId, Guid projectId, Guid importId, Guid fileId, string extension = ".bin");
}
