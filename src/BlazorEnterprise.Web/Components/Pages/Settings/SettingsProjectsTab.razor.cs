using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Infrastructure.Security;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsProjectsTab : ComponentBase, IDisposable
{
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private IProgramService ProgramService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IAuditService AuditService { get; set; } = default!;
    [Inject] private IProjectAuthorizationService ProjectAuth { get; set; } = default!;
    [Inject] private IImpersonationService ImpersonationService { get; set; } = default!;
    [Inject] private CurrentUserAuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IUnsavedChangesStateService UnsavedChanges { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;

    public record TenantFilterOption(string Id, string Name);
    public record ProgramFilterOption(string Id, string Name);

    private List<TenantFilterOption> _availableTenants = new();
    private List<ProgramFilterOption> _availablePrograms = [];
    private List<ProgramFilterOption> _assignablePrograms = [];
    private string? _selectedTenantId;
    private string? _selectedProgramId;
    private string _selectedTenantName = string.Empty;
    private List<ProjectEntity> _projects = new();
    private string _projectSearchText = string.Empty;
    private bool _isLoading;
    private string? _statusMessage;
    private bool _isError;

    // Selected project sub-tabs state
    private ProjectEntity? _selectedProject;
    private string _activeProjectTab = "tab-details";

    // Edit details state
    private bool _isEditingDetails;
    private string _editName = string.Empty;
    private string _editShortCode = string.Empty;
    private string? _editLocation;
    private ProjectStatusEnum _editStatus = ProjectStatusEnum.New;
    private string? _editProgramId;
    private DateTime? _editStartDate;
    private DateTime? _editEndDate;
    private string? _editScopeSummary;

    // Roles tab state
    private List<ProjectUserRoleEntity> _members = new();
    private string _memberFilterText = string.Empty;
    private bool _isLoadingMembers;
    private bool _showAssignRoleModal;
    private bool _isEditingRole;
    private List<UserEntity> _tenantUsers = new();
    private string? _selectedTenantUserId;
    private string _formUserId = string.Empty;
    private string _formUserName = string.Empty;
    private string _formEmail = string.Empty;
    private UserTypeEnum _formUserType = UserTypeEnum.CompanyUser;
    private ProjectRoleEnum _formRole = ProjectRoleEnum.Contribute;

    // Impersonation state
    private bool _showImpersonateModal;
    private string _impersonateTargetUserId = string.Empty;
    private string _impersonateTargetName = string.Empty;
    private string _impersonateTargetEmail = string.Empty;
    private string _impersonateReason = string.Empty;

    private IQueryable<ProjectEntity> FilteredProjects
    {
        get
        {
            var query = _projects.AsQueryable();
            if (!string.IsNullOrWhiteSpace(_selectedProgramId) && Guid.TryParse(_selectedProgramId, out var filterProgId))
            {
                query = query.Where(p => p.ProgramId == filterProgId);
            }
            if (!string.IsNullOrWhiteSpace(_projectSearchText))
            {
                var term = _projectSearchText.Trim();
                query = query.Where(p =>
                    p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.ShortCode != null && p.ShortCode.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Location != null && p.Location.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (p.ScopeSummary != null && p.ScopeSummary.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Description != null && p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }
            return query;
        }
    }

    private IQueryable<ProjectUserRoleEntity> FilteredMembers
    {
        get
        {
            var query = _members.AsQueryable();
            if (!string.IsNullOrWhiteSpace(_memberFilterText))
            {
                var term = _memberFilterText.Trim();
                query = query.Where(m =>
                    (m.UserName != null && m.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (m.UserEmail != null && m.UserEmail.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (m.UserId != null && m.UserId.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    m.Role.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            return query;
        }
    }

    private bool IsEditDetailsValid =>
        !string.IsNullOrWhiteSpace(_editName) &&
        !string.IsNullOrWhiteSpace(_editShortCode) &&
        (!_editStartDate.HasValue || !_editEndDate.HasValue || _editEndDate.Value >= _editStartDate.Value);

    protected override async Task OnInitializedAsync()
    {
        TimeZoneService.OnChange += HandleTimeZoneChanged;
        await LoadAvailableTenantsAsync();
    }

    private async Task LoadAvailableTenantsAsync()
    {
        if (CurrentUser.IsRootAdmin)
        {
            var dbTenants = (await TenantService.GetAllTenantsAsync())
                .Where(t => t.IsActive && t.Slug != "default")
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(t.Slug, t.Name))
                .ToList();

            _availableTenants = dbTenants;
        }
        else
        {
            _availableTenants = TenantContext.AllowedTenants
                .Where(t => t.IsActive && t.Slug != "default")
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(t.Id.ToString(), t.Name))
                .ToList();
        }

        if (_availableTenants.Count == 1)
        {
            await OnTenantSelected(_availableTenants[0].Id);
        }
    }

    private void OnProgramSelected(string? progId)
    {
        _selectedProgramId = string.IsNullOrWhiteSpace(progId) ? null : progId;
    }

    private void ClearProgramFilter()
    {
        _selectedProgramId = null;
    }

    private async Task OnTenantSelected(string? tenantId)
    {
        _selectedTenantId = tenantId;
        _selectedTenantName = _availableTenants.FirstOrDefault(t => t.Id == tenantId)?.Name ?? tenantId ?? string.Empty;
        _selectedProject = null;
        _selectedProgramId = null;
        _availablePrograms.Clear();
        _assignablePrograms.Clear();
        _members.Clear();

        if (!string.IsNullOrWhiteSpace(_selectedTenantId))
        {
            await LoadProjectsForTenantAsync();
        }
        else
        {
            _projects.Clear();
        }
    }

    private void ClearTenantFilter()
    {
        _selectedTenantId = null;
        _selectedTenantName = string.Empty;
        _selectedProject = null;
        _selectedProgramId = null;
        _availablePrograms.Clear();
        _assignablePrograms.Clear();
        _projects.Clear();
        _members.Clear();
    }

    private async Task LoadProjectsForTenantAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        _isLoading = true;
        try
        {
            try
            {
                var progs = await ProgramService.GetProgramsAsync(_selectedTenantId);
                _availablePrograms = [new ProgramFilterOption(string.Empty, "-- All Programs --")];
                _availablePrograms.AddRange(progs.Select(p => new ProgramFilterOption(p.Id.ToString(), $"{p.Code} - {p.Name}")));
                _assignablePrograms = [new ProgramFilterOption(string.Empty, "-- None (Unassigned) --")];
                _assignablePrograms.AddRange(progs.Select(p => new ProgramFilterOption(p.Id.ToString(), $"{p.Code} - {p.Name}")));
            }
            catch
            {
                _availablePrograms = [];
                _assignablePrograms = [];
            }

            var selectedGuid = Guid.TryParse(_selectedTenantId, out var g) ? g : Guid.Empty;
            _projects = (await TenantService.GetProjectsByTenantIdAsync(selectedGuid)).ToList();

            if (_selectedProject != null)
            {
                _selectedProject = _projects.FirstOrDefault(p => p.Id == _selectedProject.Id);
            }

            if (_selectedProject == null && _projects.Count == 1)
            {
                await SelectProjectAsync(_projects[0]);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SelectProjectAsync(ProjectEntity project)
    {
        _selectedProject = project;
        _isEditingDetails = false;
        _activeProjectTab = "tab-details";
        await LoadTenantUsersForProjectAsync();
        await LoadProjectMembersAsync();
    }

    private void StartEditDetails()
    {
        if (_selectedProject == null) return;
        _editName = _selectedProject.Name;
        _editShortCode = _selectedProject.ShortCode;
        _editLocation = _selectedProject.Location;
        _editStatus = _selectedProject.Status;
        _editProgramId = _selectedProject.ProgramId?.ToString() ?? string.Empty;
        _editStartDate = _selectedProject.StartDate;
        _editEndDate = _selectedProject.EndDate ?? _selectedProject.DueDate;
        _editScopeSummary = !string.IsNullOrWhiteSpace(_selectedProject.ScopeSummary) ? _selectedProject.ScopeSummary : _selectedProject.Description;
        _isEditingDetails = true;
        UnsavedChanges.RegisterChange("Settings > Projects", $"Editing project '{_selectedProject.Name}' details");
    }

    private void CancelEditDetails()
    {
        _isEditingDetails = false;
        UnsavedChanges.ClearChange("Settings > Projects");
    }

    private async Task SaveProjectDetailsAsync()
    {
        if (_selectedProject == null || string.IsNullOrWhiteSpace(_selectedTenantId) || !IsEditDetailsValid) return;

        Guid? progId = Guid.TryParse(_editProgramId, out var parsedProg) ? parsedProg : null;

        var result = await TenantService.UpdateTenantProjectAsync(
            _selectedTenantId,
            _selectedProject.Id,
            _editName,
            _editShortCode,
            _editLocation,
            _editStartDate,
            _editEndDate,
            _editScopeSummary,
            _editStatus,
            programId: progId);

        if (result.Succeeded && result.Data is not null)
        {
            _statusMessage = $"Project '{result.Data.Name}' updated successfully.";
            _isError = false;
            _isEditingDetails = false;
            UnsavedChanges.ClearChange("Settings > Projects");
            await LoadProjectsForTenantAsync();
            _selectedProject = _projects.FirstOrDefault(p => p.Id == result.Data.Id) ?? result.Data;
        }
        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to update project details.";
            _isError = true;
        }
    }

    private async Task LoadTenantUsersForProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return;
        var tenantGuid = Guid.TryParse(_selectedTenantId, out var g) ? g : Guid.Empty;
        try
        {
            _tenantUsers = (await TenantService.GetTenantUsersByIdAsync(tenantGuid)).ToList();
        }
        catch
        {
            _tenantUsers = [];
        }
    }

    private async Task LoadProjectMembersAsync()
    {
        if (_selectedProject == null) return;

        _isLoadingMembers = true;
        try
        {
            var members = await ProjectAuth.GetProjectMembersAsync(_selectedProject.Id);
            _members = members.ToList();
        }
        catch (Exception ex)
        {
            _statusMessage = $"Failed to load project members: {ex.Message}";
            _isError = true;
        }
        finally
        {
            _isLoadingMembers = false;
        }
    }

    private void OpenAddRoleModal()
    {
        _isEditingRole = false;
        var firstUser = _tenantUsers.FirstOrDefault();
        if (firstUser != null)
        {
            OnTenantUserSelected(firstUser.Id.ToString());
        }
        else
        {
            _selectedTenantUserId = null;
            _formUserId = string.Empty;
            _formUserName = string.Empty;
            _formEmail = string.Empty;
            _formUserType = UserTypeEnum.CompanyUser;
        }
        _formRole = ProjectRoleEnum.Contribute;
        _showAssignRoleModal = true;
    }

    private void EditAssignment(ProjectUserRoleEntity member)
    {
        _isEditingRole = true;
        _selectedTenantUserId = member.UserId;
        _formUserId = member.UserId;
        _formUserName = member.UserName;
        _formEmail = member.UserEmail;
        _formUserType = member.UserType;
        _formRole = member.Role;
        _showAssignRoleModal = true;
    }

    private void OnTenantUserSelected(string? userId)
    {
        _selectedTenantUserId = userId;
        var user = _tenantUsers.FirstOrDefault(u => u.Id.ToString() == userId);
        if (user != null)
        {
            _formUserId = user.Id.ToString();
            _formUserName = user.FullName;
            _formEmail = user.Email;
            _formUserType = user.UserType;
        }
        else
        {
            _formUserId = string.Empty;
            _formUserName = string.Empty;
            _formEmail = string.Empty;
        }
    }

    private async Task SaveAssignmentAsync()
    {
        if (_selectedProject == null) return;

        if (string.IsNullOrWhiteSpace(_formUserId) || string.IsNullOrWhiteSpace(_formEmail))
        {
            _statusMessage = "User ID and Email are required.";
            _isError = true;
            return;
        }

        try
        {
            var result = await ProjectAuth.AssignUserRoleAsync(
                _selectedProject.Id,
                _formUserId,
                _formEmail,
                _formUserName,
                _formUserType,
                _formRole);

            if (result.IsSuccess)
            {
                _statusMessage = $"Successfully assigned role '{_formRole}' to '{_formUserName}'. HybridCache invalidated.";
                _isError = false;
                _showAssignRoleModal = false;
                await LoadProjectMembersAsync();
            }
            else
            {
                _statusMessage = result.Error;
                _isError = true;
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error saving role assignment: {ex.Message}";
            _isError = true;
        }
    }

    private async Task RevokeAssignmentAsync(string userId)
    {
        if (_selectedProject == null) return;

        try
        {
            var result = await ProjectAuth.RevokeUserRoleAsync(_selectedProject.Id, userId);
            if (result.IsSuccess)
            {
                _statusMessage = $"Access revoked for user '{userId}'. Cache purged.";
                _isError = false;
                await LoadProjectMembersAsync();
            }
            else
            {
                _statusMessage = result.Error;
                _isError = true;
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error revoking role: {ex.Message}";
            _isError = true;
        }
    }

    private void PromptImpersonation(ProjectUserRoleEntity member)
    {
        _impersonateTargetUserId = member.UserId;
        _impersonateTargetName = member.UserName;
        _impersonateTargetEmail = member.UserEmail;
        _impersonateReason = string.Empty;
        _showImpersonateModal = true;
    }

    private async Task StartImpersonationAsync()
    {
        if (string.IsNullOrWhiteSpace(_impersonateReason)) return;

        var result = await ImpersonationService.StartImpersonationAsync(
            _impersonateTargetUserId,
            _impersonateReason,
            durationMinutes: 60);

        if (result.IsSuccess)
        {
            if (CurrentUser is AppCurrentUser appUser)
            {
                var adminId = appUser.IsAuthenticated ? appUser.UserId : string.Empty;
                appUser.SetImpersonation(_impersonateTargetUserId, _impersonateTargetEmail, _impersonateTargetName, adminId);
            }

            AuthStateProvider.NotifyUserChanged();
            _showImpersonateModal = false;
            NavigationManager.NavigateTo("/", forceLoad: false);
        }
        else
        {
            _statusMessage = result.Error;
            _isError = true;
        }
    }

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
        UnsavedChanges.ClearChange("Settings > Projects");
    }
}
