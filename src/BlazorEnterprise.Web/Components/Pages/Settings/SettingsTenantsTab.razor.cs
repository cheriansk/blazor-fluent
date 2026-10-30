using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using TenantInfo = BlazorEnterprise.Core.Contracts.TenantInfo;
using TaskStatusEnum = BlazorEnterprise.Shared.DataListTypes.TaskStatusEnum;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsTenantsTab : ComponentBase, IDisposable
{
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private IProgramService ProgramService { get; set; } = default!;
    [Inject] private IHierarchyAuthorizationService HierarchyAuth { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUnsavedChangesStateService UnsavedChanges { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private IUserTaskService TaskService { get; set; } = default!;

private bool IsRootAdmin => CurrentUser.IsRootAdmin;
    private List<TenantEntity> _tenants = [];
    private bool _isLoading = true;
    private string? _statusMessage;
    private bool _isError;

    // Search filters for Grids
    private string _userSearchText = string.Empty;

    private IQueryable<UserEntity> FilteredTenantUsers
    {
        get
        {
            var query = _tenantUsers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(_userSearchText))
            {
                var term = _userSearchText.Trim();
                query = query.Where(u =>
                    u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            return query;
        }
    }

    // Add tenant modal state
    private bool _showAddModal;
    private bool _showConfirmCreate;
    private string _newSlug = string.Empty;
    private string _newName = string.Empty;
    private List<string> _newInternalDomains = [];
    private string _tempInternalDomain = string.Empty;
    private List<string> _newExternalDomains = [];
    private string _tempExternalDomain = string.Empty;
    private DateTime? _newStartDate = DateTime.UtcNow.Date;
    private DateTime? _newEndDate = DateTime.UtcNow.Date.AddYears(1);
    private string _newAdminFullName = string.Empty;
    private string _newAdminEmail = string.Empty;

    // Edit / View tenant modal state
    private CskDialog? _tenantDetailsDialog;
    private TenantEntity? _selectedTenantModal;
    private bool _isModalEditMode;
    private DateTime? _editStartDate;
    private DateTime? _editEndDate;

    // Tenant user management state
    private TenantEntity? _managingUsersTenant;
    private List<UserEntity> _tenantUsers = [];
    private bool _isLoadingUsers;
    private bool _showAddUserForm;

    // Add user form inputs
    private string _userFullName = string.Empty;
    private string _userEmail = string.Empty;
    private UserTypeEnum? _detectedUserType;
    private string? _userDomainError;
    private DateTime? _userStartDate = DateTime.UtcNow.Date;
    private DateTime? _userEndDate = DateTime.UtcNow.Date.AddYears(1);
    private bool _userIsActive = true;

    // Edit user form state
    private UserEntity? _editingUser;
    private string _editUserFullName = string.Empty;
    private string _editUserEmail = string.Empty;
    private UserTypeEnum? _editDetectedUserType;
    private string? _editUserDomainError;
    private DateTime? _editUserStartDate;
    private DateTime? _editUserEndDate;
    private bool _editUserIsActive;

    private bool _canEditTenants = false;

    protected override async Task OnInitializedAsync()
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser.UserId : string.Empty;
        _canEditTenants = IsRootAdmin || (TenantContext.HasActiveTenant && !string.IsNullOrWhiteSpace(userId) && await HierarchyAuth.CanEditTenantSettingsAsync(TenantContext.TenantId, userId));
        await LoadTenantsAsync();
    }

    private async Task LoadTenantsAsync()
    {
        _isLoading = true;
        try
        {
            if (IsRootAdmin)
            {
                var result = await TenantService.GetAllTenantsAsync();
                _tenants = result.ToList();
            }
            else if (TenantContext.HasActiveTenant && !string.IsNullOrWhiteSpace(TenantContext.TenantSlug))
            {
                var activeTenant = await TenantService.GetTenantByIdentifierAsync(TenantContext.TenantSlug);
                _tenants = activeTenant != null ? [activeTenant] : [];
            }
            else
            {
                _tenants = [];
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string NormalizeDomain(string domain)
    {
        var trimmed = domain.Trim().ToLowerInvariant();
        return trimmed.StartsWith('@') ? trimmed : "@" + trimmed;
    }

    private void AddInternalDomain()
    {
        if (string.IsNullOrWhiteSpace(_tempInternalDomain)) return;
        var normalized = NormalizeDomain(_tempInternalDomain);

        if (_newInternalDomains.Any(d => d.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            _statusMessage = $"Internal domain '{normalized}' is already added.";
            _isError = true;
            return;
        }

        if (_newExternalDomains.Any(d => d.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            _statusMessage = $"Domain '{normalized}' cannot exist in both Internal and External lists.";
            _isError = true;
            return;
        }

        if (_newInternalDomains.Count >= 5)
        {
            _statusMessage = "Maximum of 5 Internal email domains reached.";
            _isError = true;
            return;
        }

        _newInternalDomains.Add(normalized);
        _tempInternalDomain = string.Empty;
        _statusMessage = null;
        _isError = false;
    }

    private void RemoveInternalDomain(string domain)
    {
        _newInternalDomains.RemoveAll(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase));
    }

    private void AddExternalDomain()
    {
        if (string.IsNullOrWhiteSpace(_tempExternalDomain)) return;
        var normalized = NormalizeDomain(_tempExternalDomain);

        if (_newExternalDomains.Any(d => d.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            _statusMessage = $"External domain '{normalized}' is already added.";
            _isError = true;
            return;
        }

        if (_newInternalDomains.Any(d => d.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            _statusMessage = $"Domain '{normalized}' cannot exist in both Internal and External lists.";
            _isError = true;
            return;
        }

        if (_newExternalDomains.Count >= 5)
        {
            _statusMessage = "Maximum of 5 External email domains reached.";
            _isError = true;
            return;
        }

        _newExternalDomains.Add(normalized);
        _tempExternalDomain = string.Empty;
        _statusMessage = null;
        _isError = false;
    }

    private void RemoveExternalDomain(string domain)
    {
        _newExternalDomains.RemoveAll(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase));
    }

    private void PromptConfirmCreate()
    {
        if (string.IsNullOrWhiteSpace(_newSlug) || string.IsNullOrWhiteSpace(_newName))
        {
            _statusMessage = "Slug and Display Name are required.";
            _isError = true;
            return;
        }

        if (!_newInternalDomains.Any())
        {
            _statusMessage = "At least one Internal email domain is required (e.g. @microsoft.com).";
            _isError = true;
            return;
        }

        if (!_newExternalDomains.Any())
        {
            _statusMessage = "At least one External email domain is required (e.g. @salesforce.com).";
            _isError = true;
            return;
        }

        var overlap = _newInternalDomains.Intersect(_newExternalDomains, StringComparer.OrdinalIgnoreCase).ToList();
        if (overlap.Any())
        {
            _statusMessage = $"Internal and External email domains cannot overlap: {string.Join(", ", overlap)}.";
            _isError = true;
            return;
        }

        if (!_newStartDate.HasValue || !_newEndDate.HasValue)
        {
            _statusMessage = "Both Start Date and End Date are strictly required.";
            _isError = true;
            return;
        }

        if (_newEndDate.Value < _newStartDate.Value)
        {
            _statusMessage = "End Date cannot precede Start Date.";
            _isError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(_newAdminFullName))
        {
            _statusMessage = "Tenant Administrator Full Name is required.";
            _isError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(_newAdminEmail))
        {
            _statusMessage = "Tenant Administrator Email is required.";
            _isError = true;
            return;
        }

        var normalizedEmail = _newAdminEmail.Trim().ToLowerInvariant();
        var matchesInternal = _newInternalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase));
        var matchesExternal = _newExternalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase));

        if (!matchesInternal && !matchesExternal)
        {
            _statusMessage = $"Administrator email '{normalizedEmail}' must match one of the configured Internal domains ({string.Join(", ", _newInternalDomains)}) or External domains ({string.Join(", ", _newExternalDomains)}).";
            _isError = true;
            return;
        }

        _statusMessage = null;
        _isError = false;
        _showConfirmCreate = true;
    }

    private async Task CreateTenantAsync()
    {
        if (!IsRootAdmin) return;

        var result = await TenantService.CreateTenantWithAdminAsync(
            _newSlug,
            _newName,
            _newInternalDomains,
            _newExternalDomains,
            _newStartDate!.Value,
            _newEndDate!.Value,
            _newAdminFullName,
            _newAdminEmail);

        if (result.Succeeded && result.Data is not null)
        {
            _statusMessage = $"Tenant '{result.Data.Name} ({result.Data.Slug})' created successfully with Tenant Administrator '{_newAdminEmail}'.";
            _isError = false;
            CancelAdd();
            await LoadTenantsAsync();
        }
        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to create tenant.";
            _isError = true;
            _showConfirmCreate = false;
        }
    }

    private void StartAddTenant()
    {
        _showAddModal = true;
        _showConfirmCreate = false;
        UnsavedChanges.RegisterChange("Settings > Tenants", "Provisioning new tenant");
    }

    private void CancelAdd()
    {
        _showAddModal = false;
        _showConfirmCreate = false;
        _newSlug = string.Empty;
        _newName = string.Empty;
        _newInternalDomains.Clear();
        _tempInternalDomain = string.Empty;
        _newExternalDomains.Clear();
        _tempExternalDomain = string.Empty;
        _newStartDate = DateTime.UtcNow.Date;
        _newEndDate = DateTime.UtcNow.Date.AddYears(1);
        _newAdminFullName = string.Empty;
        _newAdminEmail = string.Empty;
        UnsavedChanges.ClearChange("Settings > Tenants");
    }

    private async Task ToggleStatusAsync(TenantEntity tenant, bool isActive)
    {
        if (!IsRootAdmin) return;

        var result = await TenantService.UpdateTenantStatusAsync(tenant.Id, isActive);
        if (result.Succeeded)
        {
            tenant.IsActive = isActive;
            _statusMessage = $"Tenant '{tenant.Name}' status changed to {(isActive ? "Active" : "Inactive")}.";
            _isError = false;
        }
        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to update tenant status.";
            _isError = true;
        }
    }

    private async Task OpenTenantModalAsync(TenantEntity tenant, bool isEdit)
    {
        if (isEdit && !IsRootAdmin && !_canEditTenants) return;

        _selectedTenantModal = tenant;
        _isModalEditMode = isEdit;
        _editStartDate = tenant.StartDate;
        _editEndDate = tenant.EndDate;
        if (isEdit)
        {
            UnsavedChanges.RegisterChange("Settings > Tenants", $"Editing contract dates for '{tenant.Name}'");
        }

        if (_tenantDetailsDialog is not null)
        {
            await _tenantDetailsDialog.ShowAsync();
        }
    }

    private async Task CloseTenantModalAsync()
    {
        if (_tenantDetailsDialog is not null)
        {
            await _tenantDetailsDialog.HideAsync();
        }
        _selectedTenantModal = null;
        _isModalEditMode = false;
        UnsavedChanges.ClearChange("Settings > Tenants");
    }

    private async Task SaveDatesAsync()
    {
        if ((!IsRootAdmin && !_canEditTenants) || _selectedTenantModal is null) return;

        if (!_editStartDate.HasValue || !_editEndDate.HasValue)
        {
            _statusMessage = "Both Start Date and End Date are strictly required.";
            _isError = true;
            return;
        }

        if (_editEndDate.Value < _editStartDate.Value)
        {
            _statusMessage = "End Date cannot precede Start Date.";
            _isError = true;
            return;
        }

        var result = await TenantService.UpdateTenantDatesAsync(_selectedTenantModal.Id, _editStartDate.Value, _editEndDate.Value);
        if (result.Succeeded)
        {
            _selectedTenantModal.StartDate = _editStartDate.Value;
            _selectedTenantModal.EndDate = _editEndDate.Value;
            _statusMessage = $"Contract dates updated successfully for '{_selectedTenantModal.Name}'.";
            _isError = false;
            await CloseTenantModalAsync();
        }
        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to update contract dates.";
            _isError = true;
        }
    }

    // --- Tenant User Management ---
    private async Task OpenUserManagement(TenantEntity tenant)
    {
        if (!IsRootAdmin && !_canEditTenants) return;

        _managingUsersTenant = tenant;
        _showAddUserForm = false;
        _editingUser = null;
        _userFullName = string.Empty;
        _userEmail = string.Empty;
        _detectedUserType = null;
        _userDomainError = null;
        _userStartDate = DateTime.UtcNow.Date;
        _userEndDate = DateTime.UtcNow.Date.AddYears(1);
        _userIsActive = true;
        _userSearchText = string.Empty;

        await LoadTenantUsersAsync();
    }

    private void OpenAddUserForm()
    {
        _editingUser = null;
        _showAddUserForm = true;
    }

    private void CloseUserManagement()
    {
        _managingUsersTenant = null;
        _tenantUsers.Clear();
        _showAddUserForm = false;
        _editingUser = null;
    }

    private bool _isDaemonActive = true;

    private async Task LoadTenantUsersAsync()
    {
        if (_managingUsersTenant is null) return;

        _isLoadingUsers = true;
        try
        {
            var users = await TenantService.GetTenantUsersAsync(_managingUsersTenant.Slug);
            _tenantUsers = users.ToList();

            var daemonRes = await TenantService.IsTenantDaemonActiveAsync(_managingUsersTenant.Id);
            _isDaemonActive = daemonRes.Succeeded ? daemonRes.Value : true;
        }
        finally
        {
            _isLoadingUsers = false;
        }
    }

    private async Task ToggleDaemonStatusAsync(bool isActive)
    {
        if (_managingUsersTenant is null) return;
        if (!IsRootAdmin && !_canEditTenants) return;

        var result = await TenantService.SetTenantDaemonActiveAsync(_managingUsersTenant.Id, isActive);
        if (result.Succeeded)
        {
            _isDaemonActive = isActive;
            _statusMessage = $"Automated background processing for '{_managingUsersTenant.Name}' is now {(isActive ? "Active" : "Suspended")}.";
            _isError = false;
        }
        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to update background processing status.";
            _isError = true;
        }
    }

    private void OnUserEmailChanged(string? value)
    {
        _userEmail = value ?? string.Empty;
        if (_managingUsersTenant is null || string.IsNullOrWhiteSpace(_userEmail))
        {
            _detectedUserType = null;
            _userDomainError = null;
            return;
        }

        var normalizedEmail = _userEmail.Trim().ToLowerInvariant();
        var internalDomains = _managingUsersTenant.GetInternalDomains();
        var externalDomains = _managingUsersTenant.GetExternalDomains();

        if (internalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            _detectedUserType = UserTypeEnum.CompanyUser;
            _userDomainError = null;
        }
        else if (externalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            _detectedUserType = UserTypeEnum.ClientUser;
            _userDomainError = null;
        }
        else
        {
            _detectedUserType = null;
            _userDomainError = $"Email must end with configured Internal domains ({string.Join(", ", internalDomains)}) or External domains ({string.Join(", ", externalDomains)}).";
        }
    }

    private string GetUserTypeDisplay()
    {
        return _detectedUserType switch
        {
            UserTypeEnum.CompanyUser => "Internal Staff (CompanyUser)",
            UserTypeEnum.ClientUser => "External Client (ClientUser)",
            _ => "(Enter email matching configured domain)"
        };
    }

    private bool IsUserFormValid =>
        !string.IsNullOrWhiteSpace(_userFullName) &&
        !string.IsNullOrWhiteSpace(_userEmail) &&
        _detectedUserType.HasValue &&
        _userStartDate.HasValue &&
        _userEndDate.HasValue &&
        _userEndDate.Value >= _userStartDate.Value;

    private async Task CreateTenantUserAsync()
    {
        if ((!IsRootAdmin && !_canEditTenants) || _managingUsersTenant is null || !IsUserFormValid) return;

        var result = await TenantService.AddTenantUserAsync(
            _managingUsersTenant.Slug,
            _userFullName,
            _userEmail,
            _userStartDate!.Value,
            _userEndDate!.Value,
            _userIsActive);

        if (result.Succeeded && result.Data is not null)
        {
            _statusMessage = $"User '{result.Data.FullName}' ({result.Data.Email}) added to tenant '{_managingUsersTenant.Name}'.";
            _isError = false;
            _showAddUserForm = false;
            _userFullName = string.Empty;
            _userEmail = string.Empty;
            _detectedUserType = null;
            _userDomainError = null;
            await LoadTenantUsersAsync();
        }
        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to add tenant user.";
            _isError = true;
        }
    }

    // --- Edit User Functionality ---
    private void StartEditUser(UserEntity user)
    {
        _showAddUserForm = false;
        _editingUser = user;
        _editUserFullName = user.FullName;
        _editUserEmail = user.Email;
        _editDetectedUserType = user.UserType;
        _editUserDomainError = null;
        _editUserStartDate = user.StartDate.ToLocalTime().Date;
        _editUserEndDate = user.EndDate?.ToLocalTime().Date;
        _editUserIsActive = user.IsActive;
    }

    private void OnEditUserEmailChanged(string? value)
    {
        _editUserEmail = value ?? string.Empty;
        if (_managingUsersTenant is null || string.IsNullOrWhiteSpace(_editUserEmail))
        {
            _editDetectedUserType = null;
            _editUserDomainError = null;
            return;
        }

        var normalizedEmail = _editUserEmail.Trim().ToLowerInvariant();
        var internalDomains = _managingUsersTenant.GetInternalDomains();
        var externalDomains = _managingUsersTenant.GetExternalDomains();

        if (internalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            _editDetectedUserType = UserTypeEnum.CompanyUser;
            _editUserDomainError = null;
        }
        else if (externalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            _editDetectedUserType = UserTypeEnum.ClientUser;
            _editUserDomainError = null;
        }
        else
        {
            _editDetectedUserType = null;
            _editUserDomainError = $"Email must end with configured Internal ({string.Join(", ", internalDomains)}) or External ({string.Join(", ", externalDomains)}) domains.";
        }
    }

    private string GetEditUserTypeDisplay()
    {
        return _editDetectedUserType switch
        {
            UserTypeEnum.CompanyUser => "Internal Staff (CompanyUser)",
            UserTypeEnum.ClientUser => "External Client (ClientUser)",
            _ => "(Enter email matching configured domain)"
        };
    }

    private bool IsEditUserFormValid =>
        _editingUser is not null &&
        !string.IsNullOrWhiteSpace(_editUserFullName) &&
        !string.IsNullOrWhiteSpace(_editUserEmail) &&
        _editDetectedUserType.HasValue &&
        _editUserStartDate.HasValue &&
        _editUserEndDate.HasValue &&
        _editUserEndDate.Value >= _editUserStartDate.Value;

    private async Task SaveEditUserAsync()
    {
        if (!IsRootAdmin || _managingUsersTenant is null || _editingUser is null || !IsEditUserFormValid) return;

        var result = await TenantService.UpdateTenantUserAsync(
            _managingUsersTenant.Slug,
            _editingUser.Id,
            _editUserFullName,
            _editUserEmail,
            _editUserStartDate!.Value,
            _editUserEndDate!.Value,
            _editUserIsActive);

        if (result.Succeeded && result.Data is not null)
        {
            _statusMessage = $"User '{result.Data.FullName}' updated successfully.";
            _isError = false;
            _editingUser = null;
            await LoadTenantUsersAsync();
        }
        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to update tenant user.";
            _isError = true;
        }
    }

    // --- Tenant Program Management ---
    private TenantEntity? _managingProgramsTenant;
    private List<ProgramEntity> _tenantPrograms = [];
    private bool _isLoadingPrograms;
    private bool _showAddProgramForm;
    private ProgramEntity? _editingProgram;

    // Program form inputs
    private string _progCode = string.Empty;
    private string _progName = string.Empty;
    private string _progDescription = string.Empty;
    private ProgramStatusEnum _progStatus = ProgramStatusEnum.Active;
    private DateTime? _progStartDate;
    private DateTime? _progEndDate;
    private string _programSearchText = string.Empty;

    private IQueryable<ProgramEntity> FilteredTenantPrograms
    {
        get
        {
            var query = _tenantPrograms.AsQueryable();
            if (!string.IsNullOrWhiteSpace(_programSearchText))
            {
                var term = _programSearchText.Trim();
                query = query.Where(p =>
                    p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.Code != null && p.Code.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Description != null && p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }
            return query;
        }
    }

    private bool IsProgramFormValid =>
        !string.IsNullOrWhiteSpace(_progCode) &&
        !string.IsNullOrWhiteSpace(_progName) &&
        (!_progStartDate.HasValue || !_progEndDate.HasValue || _progEndDate.Value >= _progStartDate.Value);

    private async Task OpenProgramManagement(TenantEntity tenant)
    {
        _managingProgramsTenant = tenant;
        _managingUsersTenant = null;
        _showAddProgramForm = false;
        _editingProgram = null;
        _programSearchText = string.Empty;
        await LoadTenantProgramsAsync();
    }

    private void CloseProgramManagement()
    {
        _managingProgramsTenant = null;
        _showAddProgramForm = false;
        _editingProgram = null;
        _tenantPrograms.Clear();
    }

    private async Task LoadTenantProgramsAsync()
    {
        if (_managingProgramsTenant is null) return;
        _isLoadingPrograms = true;
        try
        {
            var programs = await ProgramService.GetProgramsAsync(_managingProgramsTenant.Slug);
            _tenantPrograms = programs.ToList();
        }
        finally
        {
            _isLoadingPrograms = false;
        }
    }

    private void OpenAddProgramForm()
    {
        _editingProgram = null;
        _progCode = string.Empty;
        _progName = string.Empty;
        _progDescription = string.Empty;
        _progStatus = ProgramStatusEnum.Active;
        _progStartDate = DateTime.UtcNow.Date;
        _progEndDate = DateTime.UtcNow.Date.AddMonths(12);
        _showAddProgramForm = true;
    }

    private void StartEditProgram(ProgramEntity program)
    {
        _editingProgram = program;
        _progCode = program.Code;
        _progName = program.Name;
        _progDescription = program.Description;
        _progStatus = program.Status;
        _progStartDate = program.StartDate;
        _progEndDate = program.EndDate ?? program.DueDate;
        _showAddProgramForm = true;
    }

    private void CloseProgramForm()
    {
        _showAddProgramForm = false;
        _editingProgram = null;
    }

    private async Task SaveTenantProgramAsync()
    {
        if (_managingProgramsTenant is null || !IsProgramFormValid) return;

        if (_editingProgram is null)
        {
            var result = await ProgramService.CreateProgramAsync(
                _managingProgramsTenant.Slug,
                _progName,
                _progCode,
                _progDescription,
                _progStartDate,
                _progEndDate,
                _progStatus);

            if (result.Succeeded && result.Data is not null)
            {
                _statusMessage = $"Program '{result.Data.Name}' ({result.Data.Code}) created successfully.";
                _isError = false;
                CloseProgramForm();
                await LoadTenantProgramsAsync();
            }
            else
            {
                _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to create program.";
                _isError = true;
            }
        }
        else
        {
            var result = await ProgramService.UpdateProgramAsync(
                _editingProgram.Id,
                _progName,
                _progCode,
                _progDescription,
                _progStartDate,
                _progEndDate,
                _progStatus,
                isActive: true);

            if (result.Succeeded && result.Data is not null)
            {
                _statusMessage = $"Program '{result.Data.Name}' updated successfully.";
                _isError = false;
                CloseProgramForm();
                await LoadTenantProgramsAsync();
            }
            else
            {
                _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to update program.";
                _isError = true;
            }
        }
    }

    // ------------------------------------------------------------------------
    // Tenant-Level Tasks & Risks Management
    // ------------------------------------------------------------------------
    private TenantEntity? _managingTasksTenant;
    private string _activeTenantWorkItemTab = "tasks";
    private List<TaskEntity> _tenantTasks = new();
    private List<RiskEntity> _tenantRisks = new();
    private bool _loadingTenantWorkItems;

    // Quick Add Task Form
    private string _newTenantTaskTitle = string.Empty;
    private string _newTenantTaskDesc = string.Empty;
    private TaskPriorityEnum _newTenantTaskPriority = TaskPriorityEnum.Medium;
    private DateTime? _newTenantTaskDueDate;

    // Quick Add Risk Form
    private string _newTenantRiskTitle = string.Empty;
    private string _newTenantRiskDesc = string.Empty;
    private RiskProbabilityEnum _newTenantRiskProb = RiskProbabilityEnum.Medium;
    private RiskImpactEnum _newTenantRiskImpact = RiskImpactEnum.Medium;

    private async Task OpenTenantTasksManagement(TenantEntity tenant)
    {
        _managingTasksTenant = tenant;
        _managingProgramsTenant = null;
        _managingUsersTenant = null;
        await LoadTenantWorkItemsAsync();
    }

    private void CloseTenantTasksManagement()
    {
        _managingTasksTenant = null;
        _tenantTasks.Clear();
        _tenantRisks.Clear();
    }

    private async Task LoadTenantWorkItemsAsync()
    {
        if (_managingTasksTenant == null) return;
        _loadingTenantWorkItems = true;
        try
        {
            _tenantTasks = (await TaskService.GetTenantTasksAsync(_managingTasksTenant.Id)).ToList();
            _tenantRisks = (await TaskService.GetTenantRisksAsync(_managingTasksTenant.Id)).ToList();
        }
        catch (Exception ex)
        {
            _statusMessage = $"Failed to load tenant work items: {ex.Message}";
            _isError = true;
        }
        finally
        {
            _loadingTenantWorkItems = false;
        }
    }

    private async Task CreateTenantTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTenantTaskTitle) || _managingTasksTenant == null) return;
        try
        {
            var task = new TaskEntity
            {
                TenantId = _managingTasksTenant.Id,
                Level = TaskLevelEnum.Tenant,
                Title = _newTenantTaskTitle.Trim(),
                Description = _newTenantTaskDesc?.Trim() ?? string.Empty,
                Priority = _newTenantTaskPriority,
                Status = TaskStatusEnum.Open,
                DueDate = _newTenantTaskDueDate ?? DateTime.UtcNow.AddDays(7),
                StartDate = DateTime.UtcNow,
                CreatedBy = CurrentUser.Email ?? "system"
            };
            var taskResult = await TaskService.CreateTaskAsync(task);
            if (!taskResult.IsSuccess)
            {
                _statusMessage = $"Error creating tenant task: {taskResult.Error}";
                _isError = true;
                return;
            }
            _newTenantTaskTitle = string.Empty;
            _newTenantTaskDesc = string.Empty;
            _newTenantTaskDueDate = null;
            _statusMessage = "Tenant task created successfully.";
            _isError = false;
            await LoadTenantWorkItemsAsync();
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error creating tenant task: {ex.Message}";
            _isError = true;
        }
    }

    private async Task CreateTenantRiskAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTenantRiskTitle) || _managingTasksTenant == null) return;
        try
        {
            var risk = new RiskEntity
            {
                TenantId = _managingTasksTenant.Id,
                Level = TaskLevelEnum.Tenant,
                Title = _newTenantRiskTitle.Trim(),
                Description = _newTenantRiskDesc?.Trim() ?? string.Empty,
                Probability = _newTenantRiskProb,
                Impact = _newTenantRiskImpact,
                RiskStatus = RiskStatusEnum.Identified,
                Priority = TaskPriorityEnum.High,
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(14),
                CreatedBy = CurrentUser.Email ?? "system",
                Status = TaskStatusEnum.Pending
            };
            var riskResult = await TaskService.CreateRiskAsync(risk);
            if (!riskResult.IsSuccess)
            {
                _statusMessage = $"Error creating tenant risk: {riskResult.Error}";
                _isError = true;
                return;
            }
            _newTenantRiskTitle = string.Empty;
            _newTenantRiskDesc = string.Empty;
            _statusMessage = "Tenant risk created successfully.";
            _isError = false;
            await LoadTenantWorkItemsAsync();
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error creating tenant risk: {ex.Message}";
            _isError = true;
        }
    }

    private async Task ToggleTenantTaskStatusAsync(TaskEntity task)
    {
        task.Status = task.Status == TaskStatusEnum.Closed ? TaskStatusEnum.Open : TaskStatusEnum.Closed;
        if (task.Status == TaskStatusEnum.Closed) task.EndDate = DateTime.UtcNow;
        else task.EndDate = null;
        task.UpdatedBy = CurrentUser.Email ?? "system";
        task.Updated = DateTime.UtcNow;
        await TaskService.UpdateTaskAsync(task);
        _statusMessage = $"Task status updated to {task.Status}.";
        _isError = false;
    }

    public void Dispose()
    {
        UnsavedChanges.ClearChange("Settings > Tenants");
    }
}