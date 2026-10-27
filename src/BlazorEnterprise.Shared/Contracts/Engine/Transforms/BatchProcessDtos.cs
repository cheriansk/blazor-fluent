using System.Text.Json.Serialization;
using BlazorEnterprise.Shared.Models;

namespace BlazorEnterprise.Shared.Contracts.Engine.Transforms;

/// <summary>
/// Compact staged row inside a canonical JSON chunk.
/// Properties are serialized with 1-3 character names ('id', 'idx', 'd') to minimize JSON footprint.
/// </summary>
public sealed record StagedRowDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("idx")] int Idx,
    [property: JsonPropertyName("d")] IReadOnlyDictionary<string, string?> D);

/// <summary>
/// High-density canonical JSON chunk stored in staging.StagedImports and transmitted to Cloud API.
/// Self-describing via HeaderMap (e.g., "c1" -> "Title").
/// </summary>
public sealed record CanonicalStagedChunkPayload(
    [property: JsonPropertyName("importId")] Guid ImportId,
    [property: JsonPropertyName("importFileId")] Guid ImportFileId,
    [property: JsonPropertyName("fileType")] string FileType,
    [property: JsonPropertyName("sheetName")] string SheetName,
    [property: JsonPropertyName("chunkIndex")] int ChunkIndex,
    [property: JsonPropertyName("totalChunks")] int TotalChunks,
    [property: JsonPropertyName("rowCount")] int RowCount,
    [property: JsonPropertyName("isLastChunk")] bool IsLastChunk,
    [property: JsonPropertyName("headerMap")] IReadOnlyDictionary<string, string> HeaderMap,
    [property: JsonPropertyName("rows")] IReadOnlyList<StagedRowDto> Rows);

/// <summary>
/// Request payload sent to POST /api/v1/transforms/process-batch.
/// Supports either inline rows or a Gzip-compressed CanonicalStagedChunkPayload byte array.
/// </summary>
public sealed record ProcessBatchReqDto(
    Guid ImportId,
    Guid ImportFileId,
    string FileType,
    int ChunkIndex,
    int TotalChunks,
    bool IsLastChunk,
    IReadOnlyDictionary<string, string> HeaderMap,
    IReadOnlyList<StagedRowDto> Rows,
    byte[]? CompressedChunkBytes = null,
    bool? ForceEncryptAtRest = null);

/// <summary>
/// Processed domain row returned by the Cloud API Rules Engine.
/// When IsEncryptedAtRest is true, EncryptedPayloadBytes holds the API-keyed AES-256-GCM ciphertext
/// and DomainProperties contains redacted placeholders ("[ENCRYPTED]").
/// </summary>
public sealed record ProcessedDomainRowDto(
    Guid SourceRowId,
    int SourceRowIndex,
    string TargetEntityName,
    string? UpsertKeyValue,
    bool IsEncryptedAtRest,
    byte[]? EncryptedPayloadBytes,
    IReadOnlyDictionary<string, string?> DomainProperties);

/// <summary>
/// Response returned by POST /api/v1/transforms/process-batch.
/// Enforces All-or-Nothing atomicity: if IsSuccess is false, ProcessedRows is empty.
/// </summary>
public sealed record ProcessBatchRespDto(
    Guid ImportFileId,
    int ChunkIndex,
    bool IsSuccess,
    bool IsEncryptedAtRest,
    IReadOnlyList<ProcessedDomainRowDto> ProcessedRows,
    IReadOnlyList<FileValidationError> Errors,
    string? ErrorMessage);

/// <summary>
/// Single encrypted record item submitted to the Cloud API for on-demand screen decryption.
/// </summary>
public sealed record DecryptProprietaryItemDto(
    Guid RecordId,
    byte[] EncryptedPayloadBytes);

/// <summary>
/// Request sent to POST /api/v1/transforms/decrypt-proprietary when rendering encrypted records on screen.
/// </summary>
public sealed record DecryptProprietaryReqDto(
    IReadOnlyList<DecryptProprietaryItemDto> Items);

/// <summary>
/// Decrypted record properties returned in-memory for UI display only (never persisted to DB).
/// </summary>
public sealed record DecryptedProprietaryItemDto(
    Guid RecordId,
    IReadOnlyDictionary<string, string?> Properties);

/// <summary>
/// Response returned by POST /api/v1/transforms/decrypt-proprietary.
/// </summary>
public sealed record DecryptProprietaryRespDto(
    bool IsSuccess,
    IReadOnlyList<DecryptedProprietaryItemDto> Items,
    string? ErrorMessage);
