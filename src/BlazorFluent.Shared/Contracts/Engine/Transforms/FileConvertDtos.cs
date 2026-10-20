using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Shared.Models;

namespace BlazorFluent.Shared.Contracts.Engine.Transforms;

public record FileConvertReqDto(
    Guid SessionId,
    ImportFileTypeEnum FileType,
    string FileName,
    byte[] FileBytes
);

public record FileConvertRespDto(
    Guid SessionId,
    bool IsSuccess,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyList<FileValidationError> Errors,
    string? HydratedJsonPayload
);
