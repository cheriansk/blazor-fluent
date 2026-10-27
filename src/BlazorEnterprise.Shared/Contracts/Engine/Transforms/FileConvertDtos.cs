using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Models;

namespace BlazorEnterprise.Shared.Contracts.Engine.Transforms;

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
