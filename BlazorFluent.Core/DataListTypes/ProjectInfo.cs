namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Lightweight project information DTO used for workspace navigation and context switching.
/// </summary>
public record ProjectInfo(
    Guid Id,
    string Name,
    string? ShortCode = null,
    ProjectStatus Status = ProjectStatus.Active);
