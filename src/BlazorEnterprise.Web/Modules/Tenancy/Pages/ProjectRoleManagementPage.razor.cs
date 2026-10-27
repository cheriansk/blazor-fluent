using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Infrastructure.Security;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Modules.Tenancy.Pages;

public partial class ProjectRoleManagementPage : ComponentBase
{
    [Inject] private IProjectAuthorizationService ProjectAuth { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IImpersonationService ImpersonationService { get; set; } = default!;
    [Inject] private CurrentUserAuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

private List<ProjectEntity> _projects = new();

    private string? _selectedProjectId;

    private List<ProjectUserRoleEntity> _members = new();

    private string _memberFilterText = string.Empty;



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



    private List<UserEntity> _tenantUsers = [];

    private string? _selectedTenantUserId;



    private bool _isLoading = false;

    private bool _showAssignModal = false;

    private bool _isEditing = false;

    private string? _statusMessage;

    private bool _isError = false;



    // Form inputs

    private string _formUserId = string.Empty;

    private string _formUserName = string.Empty;

    private string _formEmail = string.Empty;

    private UserTypeEnum _formUserType = UserTypeEnum.CompanyUser;

    private ProjectRoleEnum _formRole = ProjectRoleEnum.ReadOnly;



    protected override async Task OnInitializedAsync()

    {

        await LoadProjectsAsync();

        await LoadTenantUsersAsync();

        if (!string.IsNullOrWhiteSpace(_selectedProjectId))

        {

            await LoadMembersAsync();

        }

    }



    private async Task LoadTenantUsersAsync()

    {

        try

        {

            var tenantId = TenantContext.TenantId;

            _tenantUsers = (await TenantService.GetTenantUsersAsync(TenantContext.TenantSlug)).Where(u => u.IsActive && !u.IsDeleted).OrderBy(u => u.FullName).ToList();















        }

        catch

        {

            _tenantUsers = [];

        }

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



    private async Task LoadProjectsAsync()

    {

        try

        {

            var dbProjects = (await TenantService.GetTenantProjectsAsync(TenantContext.TenantSlug)).Where(p => !p.IsDeleted).OrderBy(p => p.Name).ToList();











            if (dbProjects.Count > 0)

            {

                _projects = dbProjects;

            }

            else

            {

                // Fallback demo projects if database has not been seeded yet

                _projects = new List<ProjectEntity>();

            }



            _selectedProjectId = _projects.FirstOrDefault()?.Id.ToString();

        }

        catch (Exception ex)

        {

            _statusMessage = $"Failed to load projects: {ex.Message}";

            _isError = true;

            _projects = new List<ProjectEntity>();

        }

    }



    private async Task OnProjectChanged(string? id)

    {

        _selectedProjectId = id;

        await LoadMembersAsync();

    }



    private async Task LoadMembersAsync()

    {

        if (string.IsNullOrWhiteSpace(_selectedProjectId) || !Guid.TryParse(_selectedProjectId, out var projectId)) return;



        _isLoading = true;

        try

        {

            var members = await ProjectAuth.GetProjectMembersAsync(projectId);

            _members = members.ToList();

        }

        catch (Exception ex)

        {

            _statusMessage = $"Failed to load members: {ex.Message}";

            _isError = true;

        }

        finally

        {

            _isLoading = false;

        }

    }



    private void OpenAddModal()

    {

        _isEditing = false;

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

        _showAssignModal = true;

    }



    private void EditAssignment(ProjectUserRoleEntity member)

    {

        _isEditing = true;

        _selectedTenantUserId = member.UserId;

        _formUserId = member.UserId;

        _formUserName = member.UserName;

        _formEmail = member.UserEmail;

        _formUserType = member.UserType;

        _formRole = member.Role;

        _showAssignModal = true;

    }



    private void CancelModal()

    {

        _showAssignModal = false;

    }



    private async Task SaveAssignmentAsync()

    {

        if (string.IsNullOrWhiteSpace(_selectedProjectId) || !Guid.TryParse(_selectedProjectId, out var projectId)) return;



        if (string.IsNullOrWhiteSpace(_formUserId) || string.IsNullOrWhiteSpace(_formEmail))

        {

            _statusMessage = "User ID and Email are required.";

            _isError = true;

            return;

        }



        try

        {

            var result = await ProjectAuth.AssignUserRoleAsync(

                projectId,

                _formUserId,

                _formEmail,

                _formUserName,

                _formUserType,

                _formRole);



            if (result.IsSuccess)

            {

                _statusMessage = $"Successfully saved role '{_formRole}' for user '{_formUserName}'. HybridCache purged.";

                _isError = false;

                _showAssignModal = false;

                await LoadMembersAsync();

            }

            else

            {

                _statusMessage = result.Error;

                _isError = true;

            }

        }

        catch (Exception ex)

        {

            _statusMessage = $"Error saving assignment: {ex.Message}";

            _isError = true;

        }

    }



    private async Task RevokeAssignmentAsync(string userId)

    {

        if (string.IsNullOrWhiteSpace(_selectedProjectId) || !Guid.TryParse(_selectedProjectId, out var projectId)) return;



        try

        {

            var result = await ProjectAuth.RevokeUserRoleAsync(projectId, userId);

            if (result.IsSuccess)

            {

                _statusMessage = $"Access revoked for user '{userId}'. Cache purged.";

                _isError = false;

                await LoadMembersAsync();

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



    // --- Impersonation ---

    private bool _showImpersonateModal = false;

    private string _impersonateTargetUserId = string.Empty;

    private string _impersonateTargetName = string.Empty;

    private string _impersonateTargetEmail = string.Empty;

    private string _impersonateReason = string.Empty;



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
}