using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Dtos.Response;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Scoped implementation of <see cref="IProjectContext"/>.
/// Manages active project and program workspace context for the current circuit.
/// </summary>
public sealed class ProjectContext : IProjectContext
{
    private readonly ILogger<ProjectContext> _logger;
    private Guid? _projectId;
    private string? _projectName;
    private string? _projectShortCode;
    private Guid? _programId;
    private string? _programName;
    private List<ProjectInfoDto> _allowedProjects = new();

    public event Action? OnChange;

    public ProjectContext(ILogger<ProjectContext>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectContext>.Instance;
    }

    public Guid? ProjectId => _projectId;
    public string? ProjectName => _projectName;
    public string? ProjectShortCode => _projectShortCode;
    public Guid? ProgramId => _programId;
    public string? ProgramName => _programName;
    public IReadOnlyList<ProjectInfoDto> AllowedProjects => _allowedProjects.AsReadOnly();

    public void Initialize(
        Guid? projectId,
        string? projectName,
        string? projectCode,
        IEnumerable<ProjectInfoDto> allowedProjects,
        Guid? programId = null,
        string? programName = null)
    {
        _allowedProjects = new List<ProjectInfoDto>(allowedProjects);
        _programId = programId;
        _programName = programName;

        // If a specific project was passed and is allowed, set it
        if (projectId.HasValue && _allowedProjects.Any(p => p.Id == projectId.Value))
        {
            var match = _allowedProjects.First(p => p.Id == projectId.Value);
            _projectId = match.Id;
            _projectName = match.Name;
            _projectShortCode = match.ShortCode;
            if (match.ProgramId.HasValue && !_programId.HasValue)
            {
                _programId = match.ProgramId.Value;
            }
        }
        else if (_allowedProjects.Count == 1)
        {
            // Auto-select if only 1 project exists
            var single = _allowedProjects[0];
            _projectId = single.Id;
            _projectName = single.Name;
            _projectShortCode = single.ShortCode;
            if (single.ProgramId.HasValue && !_programId.HasValue)
            {
                _programId = single.ProgramId.Value;
            }
        }
        else
        {
            _projectId = projectId;
            _projectName = projectName;
            _projectShortCode = projectCode;
        }

        _logger.LogInformation("Project context initialized: ProjectId={ProjectId}, Name={ProjectName}, ProgramId={ProgramId}, Count={Count}",
            _projectId, _projectName, _programId, _allowedProjects.Count);

        OnChange?.Invoke();
    }

    public Result SetActiveProject(Guid projectId)
    {
        var target = _allowedProjects.FirstOrDefault(p => p.Id == projectId);
        if (target is null)
        {
            _logger.LogWarning("Attempted unauthorized or unknown project switch: ProjectId={ProjectId}", projectId);
            return Result.Failure("Project not found or access denied.");
        }

        var oldId = _projectId;
        _projectId = target.Id;
        _projectName = target.Name;
        _projectShortCode = target.ShortCode;
        if (target.ProgramId.HasValue)
        {
            _programId = target.ProgramId.Value;
        }

        _logger.LogInformation("Switched active project from {OldId} to {NewId} ({Name})", oldId, target.Id, target.Name);
        OnChange?.Invoke();
        return Result.Success();
    }

    public void SetActiveProgram(Guid? programId, string? programName)
    {
        var oldProgId = _programId;
        _programId = programId;
        _programName = programName;

        _logger.LogInformation("Switched active program from {OldProgId} to {NewProgId} ({Name})", oldProgId, programId, programName);
        OnChange?.Invoke();
    }

    public void Reset()
    {
        _projectId = null;
        _projectName = null;
        _projectShortCode = null;
        _programId = null;
        _programName = null;
        _allowedProjects.Clear();
        _logger.LogInformation("Project context reset.");
        OnChange?.Invoke();
    }
}
