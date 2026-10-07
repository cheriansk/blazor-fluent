using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Scoped implementation of <see cref="IProjectContext"/>.
/// Manages active project workspace context for the current circuit.
/// </summary>
public sealed class ProjectContext : IProjectContext
{
    private readonly ILogger<ProjectContext> _logger;
    private Guid? _projectId;
    private string? _projectName;
    private string? _projectShortCode;
    private List<ProjectInfo> _allowedProjects = new();

    public event Action? OnChange;

    public ProjectContext(ILogger<ProjectContext>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectContext>.Instance;
    }

    public Guid? ProjectId => _projectId;
    public string? ProjectName => _projectName;
    public string? ProjectShortCode => _projectShortCode;
    public IReadOnlyList<ProjectInfo> AllowedProjects => _allowedProjects.AsReadOnly();

    public void Initialize(Guid? projectId, string? projectName, string? projectCode, IEnumerable<ProjectInfo> allowedProjects)
    {
        _allowedProjects = new List<ProjectInfo>(allowedProjects);

        // If a specific project was passed and is allowed, set it
        if (projectId.HasValue && _allowedProjects.Any(p => p.Id == projectId.Value))
        {
            var match = _allowedProjects.First(p => p.Id == projectId.Value);
            _projectId = match.Id;
            _projectName = match.Name;
            _projectShortCode = match.ShortCode;
        }
        else if (_allowedProjects.Count == 1)
        {
            // Auto-select if only 1 project exists
            var single = _allowedProjects[0];
            _projectId = single.Id;
            _projectName = single.Name;
            _projectShortCode = single.ShortCode;
        }
        else
        {
            _projectId = projectId;
            _projectName = projectName;
            _projectShortCode = projectCode;
        }

        _logger.LogInformation("Project context initialized: ProjectId={ProjectId}, Name={ProjectName}, Count={Count}",
            _projectId, _projectName, _allowedProjects.Count);

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

        _logger.LogInformation("Switched active project from {OldId} to {NewId} ({Name})", oldId, target.Id, target.Name);
        OnChange?.Invoke();
        return Result.Success();
    }

    public void Reset()
    {
        _projectId = null;
        _projectName = null;
        _projectShortCode = null;
        _allowedProjects.Clear();
        _logger.LogInformation("Project context reset.");
        OnChange?.Invoke();
    }
}
