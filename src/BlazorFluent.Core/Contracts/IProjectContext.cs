using BlazorFluent.Core.Dtos;
using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Scoped ambient context tracking the active project workspace for the current session/circuit.
/// </summary>
public interface IProjectContext
{
    /// <summary>The active project ID for the current scope/circuit. Null if no project is active.</summary>
    Guid? ProjectId { get; }

    /// <summary>The active project's display name.</summary>
    string? ProjectName { get; }

    /// <summary>The active project's short code identifier.</summary>
    string? ProjectShortCode { get; }

    /// <summary>The list of projects in the active tenant available to the current user.</summary>
    IReadOnlyList<ProjectInfoDto> AllowedProjects { get; }

    /// <summary>Whether a project is currently selected.</summary>
    bool HasProject => ProjectId.HasValue;

    /// <summary>
    /// Populates the project context for the current tenant.
    /// </summary>
    void Initialize(Guid? projectId, string? projectName, string? projectCode, IEnumerable<ProjectInfoDto> allowedProjects);

    /// <summary>
    /// Switches the active project to the specified project ID.
    /// </summary>
    Result SetActiveProject(Guid projectId);

    /// <summary>
    /// Resets the project context to uninitialized (e.g., when tenant changes or on sign out).
    /// </summary>
    void Reset();

    /// <summary>Raised whenever the active project or allowed projects list changes.</summary>
    event Action? OnChange;
}
