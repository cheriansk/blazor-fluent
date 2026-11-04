using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Dtos.Response;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Scoped ambient context tracking the active program and project workspace for the current session/circuit.
/// </summary>
public interface IProjectContext
{
    /// <summary>The active project ID for the current scope/circuit. Null if no project is active.</summary>
    Guid? ProjectId { get; }

    /// <summary>The active project's display name.</summary>
    string? ProjectName { get; }

    /// <summary>The active project's short code identifier.</summary>
    string? ProjectShortCode { get; }

    /// <summary>The active program ID governing the current project or workspace context.</summary>
    Guid? ProgramId { get; }

    /// <summary>The active program's display name.</summary>
    string? ProgramName { get; }

    /// <summary>The list of projects in the active tenant available to the current user.</summary>
    IReadOnlyList<ProjectInfoDto> AllowedProjects { get; }

    /// <summary>Whether a project is currently selected.</summary>
    bool HasProject => ProjectId.HasValue;

    /// <summary>Whether a program is currently selected.</summary>
    bool HasProgram => ProgramId.HasValue;

    /// <summary>
    /// Populates the project and program context for the current tenant.
    /// </summary>
    void Initialize(
        Guid? projectId,
        string? projectName,
        string? projectCode,
        IEnumerable<ProjectInfoDto> allowedProjects,
        Guid? programId = null,
        string? programName = null);

    /// <summary>
    /// Switches the active project to the specified project ID.
    /// </summary>
    Result SetActiveProject(Guid projectId);

    /// <summary>
    /// Switches the active program context.
    /// </summary>
    void SetActiveProgram(Guid? programId, string? programName);

    /// <summary>
    /// Resets the project and program context to uninitialized (e.g., when tenant changes or on sign out).
    /// </summary>
    void Reset();

    /// <summary>Raised whenever the active project, program, or allowed projects list changes.</summary>
    event Action? OnChange;
}
