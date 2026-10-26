using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Shared.Contracts.Engine.Transforms;

public record FileChunkStreamReqDto(
    Guid SessionId,
    int ChunkIndex,
    bool IsLastChunk,
    ImportFileTypeEnum FileType,
    string FileName,
    byte[] ChunkData
);

public record FileChunkStreamRespDto(
    Guid SessionId,
    int ChunkIndex,
    bool Acknowledged,
    int ProcessedRowsCount,
    string? Message = null
);
