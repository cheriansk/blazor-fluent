using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Dtos;

/// <summary>
/// Lightweight project information DTO used for workspace navigation and context switching.
/// </summary>
public record ProjectInfoDto(
    Guid Id,
    string Name,
    string? ShortCode = null,
    ProjectStatusEnum Status = ProjectStatusEnum.Active);
