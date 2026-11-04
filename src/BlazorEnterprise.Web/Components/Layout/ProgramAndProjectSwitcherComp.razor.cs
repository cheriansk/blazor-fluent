using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Infrastructure.Security;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Components.Layout;

public partial class ProgramAndProjectSwitcherComp : ComponentBase, IDisposable
{
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IServiceScopeFactory ScopeFactory { get; set; } = default!;
    [Inject] private IAuditService AuditService { get; set; } = default!;
    [Inject] private ProtectedSessionStorage SessionStorage { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IUnsavedChangesStateService UnsavedChanges { get; set; } = default!;
    [Inject] private ILogger<ProgramAndProjectSwitcherComp> Logger { get; set; } = default!;

    private bool _isOpen;
    private bool _isLoading = true;
    private string? _errorMessage;

    private List<ProgramEntity> _programs = new();
    private List<ProjectInfoDto> _allProjects = new();
    private List<ProjectInfoDto> _filteredProjects = new();

    private Guid? _pendingProgramId;
    private Guid? _pendingProjectId;

    public bool IsFullyLocked => _programs.Count <= 1 && _filteredProjects.Count <= 1;

    public bool HasPendingChanges
    {
        get
        {
            var progChanged = _pendingProgramId != ProjectContext.ProgramId;
            var projChanged = _pendingProjectId != ProjectContext.ProjectId;
            return progChanged || projChanged;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        ProjectContext.OnChange += HandleProjectContextChanged;
        await LoadWorkspaceDataAsync();
    }

    private void HandleProjectContextChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ProjectContext.OnChange -= HandleProjectContextChanged;
    }

    private async Task LoadWorkspaceDataAsync()
    {
        if (!TenantContext.HasActiveTenant || TenantContext.TenantId == Guid.Empty || TenantContext.IsRootTenant)
        {
            _isLoading = false;
            return;
        }

        try
        {
            _isLoading = true;
            _errorMessage = null;
            var tenantSlug = TenantContext.TenantSlug;

            using var scope = ScopeFactory.CreateScope();
            var scopedTenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            scopedTenantContext.SetTenant(TenantContext.TenantId, TenantContext.TenantSlug, TenantContext.TenantName);

            var scopedCurrentUser = scope.ServiceProvider.GetService<AppCurrentUser>();
            if (scopedCurrentUser != null)
            {
                if (CurrentUser.IsSystemDaemon)
                {
                    scopedCurrentUser.SetSystemDaemon(CurrentUser.UserName, CurrentUser.Email);
                }
                else if (CurrentUser.IsAuthenticated)
                {
                    scopedCurrentUser.SetUser(CurrentUser.UserId, CurrentUser.Email, CurrentUser.UserName, true);
                }
            }

            var scopedProgramService = scope.ServiceProvider.GetRequiredService<IProgramService>();
            var scopedProjectAuthService = scope.ServiceProvider.GetRequiredService<IProjectAuthorizationService>();
            var scopedTenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

            // 1. Load tenant programs
            var programs = await scopedProgramService.GetProgramsAsync(tenantSlug);
            _programs = programs.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();

            // 2. Load tenant projects authorized for current user
            var authorizedProjectIds = await scopedProjectAuthService.GetAuthorizedProjectIdsAsync(ProjectRoleEnum.ReadOnly);
            var projectEntities = await scopedTenantService.GetTenantProjectsAsync(tenantSlug);

            _allProjects = projectEntities
                .Where(p => !p.IsDeleted && authorizedProjectIds.Contains(p.Id))
                .Select(p => new ProjectInfoDto(p.Id, p.Name, p.ShortCode, p.Status, p.ProgramId))
                .OrderBy(p => p.Name)
                .ToList();

            // Populate current or initial pending IDs
            _pendingProgramId = ProjectContext.ProgramId;
            _pendingProjectId = ProjectContext.ProjectId;

            // Auto-select program if single program exists
            if (_programs.Count == 1)
            {
                _pendingProgramId = _programs[0].Id;
            }
            else if (!_pendingProgramId.HasValue && _programs.Count > 0)
            {
                // Default to first program if none set
                _pendingProgramId = _programs[0].Id;
            }

            ApplyProgramFilter();

            // Auto-select project if single project exists under the program
            if (_filteredProjects.Count == 1)
            {
                _pendingProjectId = _filteredProjects[0].Id;
            }
            else if (_filteredProjects.Count > 1 && (!_pendingProjectId.HasValue || !_filteredProjects.Any(p => p.Id == _pendingProjectId.Value)))
            {
                _pendingProjectId = _filteredProjects[0].Id;
            }

            // If ProjectContext was not yet initialized and we resolved selections, initialize it
            if (!ProjectContext.HasProject && _pendingProjectId.HasValue)
            {
                var matchProj = _filteredProjects.FirstOrDefault(p => p.Id == _pendingProjectId.Value);
                var matchProg = _programs.FirstOrDefault(p => p.Id == _pendingProgramId);
                ProjectContext.Initialize(
                    _pendingProjectId,
                    matchProj?.Name,
                    matchProj?.ShortCode,
                    _allProjects,
                    _pendingProgramId,
                    matchProg?.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load workspace data for tenant '{TenantSlug}'", TenantContext.TenantSlug);
            _errorMessage = "Failed to load workspace options.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyProgramFilter()
    {
        if (_pendingProgramId.HasValue)
        {
            _filteredProjects = _allProjects
                .Where(p => p.ProgramId == _pendingProgramId.Value)
                .ToList();
        }
        else
        {
            _filteredProjects = _allProjects.ToList();
        }
    }

    private void ToggleModal()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            // Reset pending selections to current active context when opening
            _pendingProgramId = ProjectContext.ProgramId ?? (_programs.Count > 0 ? _programs[0].Id : null);
            ApplyProgramFilter();
            _pendingProjectId = ProjectContext.ProjectId ?? (_filteredProjects.Count > 0 ? _filteredProjects[0].Id : null);
            if (_filteredProjects.Count == 1)
            {
                _pendingProjectId = _filteredProjects[0].Id;
            }
            _errorMessage = null;
        }
    }

    private void CloseModal()
    {
        _isOpen = false;
        _errorMessage = null;
    }

    private void OnProgramSelected(string? progIdStr)
    {
        if (Guid.TryParse(progIdStr, out var progId))
        {
            _pendingProgramId = progId;
            ApplyProgramFilter();

            // If filtered projects has 1, auto-select it; otherwise pick first or null
            if (_filteredProjects.Count == 1)
            {
                _pendingProjectId = _filteredProjects[0].Id;
            }
            else if (!_filteredProjects.Any(p => p.Id == _pendingProjectId))
            {
                _pendingProjectId = _filteredProjects.FirstOrDefault()?.Id;
            }
        }
    }

    private void OnProjectSelected(string? projIdStr)
    {
        if (Guid.TryParse(projIdStr, out var projId))
        {
            _pendingProjectId = projId;
        }
    }

    private async Task ConfirmSwitchAsync()
    {
        try
        {
            var targetProg = _programs.FirstOrDefault(p => p.Id == _pendingProgramId);
            var targetProj = _allProjects.FirstOrDefault(p => p.Id == _pendingProjectId);

            // 1. Update ProjectContext
            ProjectContext.SetActiveProgram(_pendingProgramId, targetProg?.Name);

            if (_pendingProjectId.HasValue)
            {
                var result = ProjectContext.SetActiveProject(_pendingProjectId.Value);
                if (!result.Succeeded)
                {
                    _errorMessage = result.Errors.FirstOrDefault() ?? "Project switch failed.";
                    return;
                }
            }

            // 2. Persist in Encrypted Browser Session Storage
            try
            {
                var sessionResult = await SessionStorage.GetAsync<UserSessionStorageData>("app_session");
                if (sessionResult.Success && sessionResult.Value != null)
                {
                    var updated = sessionResult.Value with
                    {
                        ActiveProjectId = _pendingProjectId,
                        ActiveProgramId = _pendingProgramId
                    };
                    await SessionStorage.SetAsync("app_session", updated);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to persist workspace selection to browser session storage.");
            }

            // 3. Clear Cache and Pending Unsaved Changes
            UnsavedChanges.ResetAll();

            // 4. Audit Log
            await AuditService.LogUserActivityAsync(
                "WorkspaceSwitched",
                $"User switched workspace to Program '{targetProg?.Name}' and Project '{targetProj?.Name}' ({_pendingProjectId}).");

            _isOpen = false;

            // 5. Force Reload All Pages with fresh workspace state
            NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply workspace switch.");
            _errorMessage = "Failed to switch workspace. Please try again.";
        }
    }

    private string GetTriggerLabel()
    {
        var progName = ProjectContext.ProgramName;
        var projName = ProjectContext.ProjectName;

        if (string.IsNullOrWhiteSpace(progName) && ProjectContext.ProgramId.HasValue)
        {
            progName = _programs.FirstOrDefault(p => p.Id == ProjectContext.ProgramId.Value)?.Name;
        }

        if (string.IsNullOrWhiteSpace(projName) && ProjectContext.ProjectId.HasValue)
        {
            projName = _allProjects.FirstOrDefault(p => p.Id == ProjectContext.ProjectId.Value)?.Name;
        }

        if (!string.IsNullOrWhiteSpace(progName) && !string.IsNullOrWhiteSpace(projName))
        {
            return $"{progName} › {projName}";
        }

        if (!string.IsNullOrWhiteSpace(progName))
        {
            return progName;
        }

        if (!string.IsNullOrWhiteSpace(projName))
        {
            return projName;
        }

        return "Select Workspace";
    }
}
