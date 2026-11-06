using BlazorEnterprise.Shared.UI.Components;

using BlazorEnterprise.Components.Common;

using BlazorEnterprise.Core.Contracts;

using BlazorEnterprise.Core.Domain.Calendar;

using BlazorEnterprise.Core.Domain.Tasks;

using BlazorEnterprise.Core.Domain.Tenancy;

using BlazorEnterprise.Shared.DataListTypes;

using BlazorEnterprise.Shared.Extensions;

using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsProgramsTab : ComponentBase, IDisposable
{
    [Inject] private ITenantContext TenantContext { get; set; } = default!;

    [Inject] private ITenantService TenantService { get; set; } = default!;

    [Inject] private IProgramService ProgramService { get; set; } = default!;

    [Inject] private ICalendarService CalendarService { get; set; } = default!;

    [Inject] private IHierarchyAuthorizationService HierarchyAuth { get; set; } = default!;

    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;

    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;

    [Inject] private IUnsavedChangesStateService UnsavedChanges { get; set; } = default!;
    [Inject] private BlazorEnterprise.Shared.Contracts.Engine.Client.ICorrelationContext CorrelationContext { get; set; } = default!;

    public record TenantFilterOption(string Id, string Name);
    private List<TenantFilterOption> _availableTenants = [];
    private string? _selectedTenantId;
    private string _selectedTenantName = string.Empty;
    private List<ProgramEntity> _programs = [];
    private HashSet<Guid> _accessibleProgramIds = [];
    private HashSet<Guid> _programAdminIds = [];
    private bool _isTenantAdmin = false;
    private bool _isLoading = false;
    private string? _statusMessage;
    private bool _isError;
    private string _programSearchText = string.Empty;
    private readonly HashSet<Guid> _expandedProgramIds = new();

    // Program Wizard Integration State

    private bool _showCreateProgramWizard;
    private bool _isProgramWizardReadOnly;
    private bool _isProgramWizardEditMode;
    private string? _programWizardTitle;
    private BlazorEnterprise.Core.Dtos.CreateProgramWizardState? _programWizardInitialState;

    // Project Wizard Integration State

    private bool _showCreateProjectWizard;
    private bool _isWizardReadOnly;
    private bool _isWizardEditMode;
    private string? _wizardTitle;
    private Guid? _editingProjectId;
    private BlazorEnterprise.Core.Dtos.CreateProjectWizardState? _wizardInitialState;
    private ProgramEntity? _selectedProgram;
    private string _activeProgramTab = "tab-health";
    private bool _isLoadingHealth = false;
    private ProgramHealthSummaryDto? _healthSummary;
    private bool _isLoadingProjects = false;
    private List<ProjectEntity> _programProjects = [];
    private bool _canManageSelectedProgramProjects = false;
    private bool _isLoadingCalendar = false;
    private DateTime _selectedCalendarDate = DateTime.UtcNow.Date;
    private IReadOnlyList<CalendarItemDto> _programCalendarItems = [];
    private string _selectedRaidCategory = "Risks";
    private List<RiskEntity> _programRisks = [];
    private List<AssumptionEntity> _programAssumptions = [];
    private List<IssueEntity> _programIssues = [];
    private List<WorkDependencyEntity> _programBlockers = [];
    private bool _showProgramModal = false;
    private Guid? _editingProgramId = null;
    private uint _editingProgramVersion = 0;
    private string _programFormCode = string.Empty;
    private string _programFormName = string.Empty;
    private string _programFormDescription = string.Empty;
    private DateTime? _programFormStartDate;
    private DateTime? _programFormTargetEndDate;
    private ProgramStatusEnum _programFormStatus = ProgramStatusEnum.Active;
    private bool _showAddProjectModal = false;
    private string _projName = string.Empty;
    private string _projShortCode = string.Empty;
    private string _projLocation = string.Empty;
    private ProjectStatusEnum _projStatus = ProjectStatusEnum.New;
    private DateTime? _projStartDate;
    private DateTime? _projEndDate;
    private string _projScopeSummary = string.Empty;

    private bool IsProgramFormValid =>

        !string.IsNullOrWhiteSpace(_programFormCode) &&

        !string.IsNullOrWhiteSpace(_programFormName);

    private bool IsProjectFormValid =>

        !string.IsNullOrWhiteSpace(_projName) &&

        !string.IsNullOrWhiteSpace(_projShortCode);

    private IQueryable<ProgramEntity> FilteredPrograms
    {
        get
        {
            var result = _programs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(_programSearchText))
            {
                result = result.Where(p =>

                    p.Name.Contains(_programSearchText, StringComparison.OrdinalIgnoreCase) ||

                    p.Code.Contains(_programSearchText, StringComparison.OrdinalIgnoreCase));
            }

            return result;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableTenantsAsync();
    }

    private async Task LoadAvailableTenantsAsync()
    {
        if (CurrentUser.IsRootAdmin)
        {
            try
            {
                var allTenants = await TenantService.GetAllTenantsAsync();

                _availableTenants = allTenants

                    .Where(t => t.IsActive)

                    .OrderBy(t => t.Name)

                    .Select(t => new TenantFilterOption(t.Slug, t.Name))

                    .ToList();
            }

            catch
            {
                _availableTenants = [];
            }
        }

        else
        {
            _availableTenants = TenantContext.AllowedTenants

                .Where(t => t.IsActive && t.Slug != "default")

                .OrderBy(t => t.Name)

                .Select(t => new TenantFilterOption(t.Slug, t.Name))

                .ToList();

            if (_availableTenants.Count == 0 && TenantContext.HasActiveTenant && !string.IsNullOrWhiteSpace(TenantContext.TenantSlug))
            {
                _availableTenants.Add(new TenantFilterOption(TenantContext.TenantSlug, TenantContext.TenantName ?? string.Empty));
            }
        }

        var defaultSlug = TenantContext.HasActiveTenant && !string.IsNullOrWhiteSpace(TenantContext.TenantSlug) ? TenantContext.TenantSlug : _availableTenants.FirstOrDefault()?.Id;

        if (!string.IsNullOrWhiteSpace(defaultSlug))
        {
            await OnTenantSelectedAsync(defaultSlug);
        }
    }

    private async Task OnTenantSelectedAsync(string? tenantSlug)
    {
        _selectedTenantId = tenantSlug;
        _selectedTenantName = _availableTenants.FirstOrDefault(t => t.Id == tenantSlug)?.Name ?? tenantSlug ?? string.Empty;
        _selectedProgram = null;
        _healthSummary = null;
        _programProjects.Clear();

        if (!string.IsNullOrWhiteSpace(_selectedTenantId))
        {
            await LoadProgramsAsync();
        }

        else
        {
            _programs.Clear();
        }
    }

    private async Task LoadProgramsAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        _isLoading = true;

        try
        {
            var userId = CurrentUser.IsAuthenticated ? CurrentUser.UserId : string.Empty;
            _isTenantAdmin = await HierarchyAuth.IsTenantAdminAsync(_selectedTenantId, userId);
            var accessible = await HierarchyAuth.GetAccessibleProgramIdsAsync(_selectedTenantId, userId);
            _accessibleProgramIds = accessible.ToHashSet();
            var allTenantPrograms = await ProgramService.GetProgramsAsync(_selectedTenantId);

            if (_isTenantAdmin || CurrentUser.IsRootAdmin)
            {
                _programs = allTenantPrograms.ToList();
            }

            else
            {
                _programs = allTenantPrograms.Where(p => _accessibleProgramIds.Contains(p.Id)).ToList();
            }

            _programAdminIds.Clear();

            foreach (var prog in _programs)
            {
                if (await HierarchyAuth.IsProgramAdminAsync(prog.Id, userId))
                {
                    _programAdminIds.Add(prog.Id);
                }
            }

            if (_selectedProgram != null)
            {
                _selectedProgram = _programs.FirstOrDefault(p => p.Id == _selectedProgram.Id);
            }
        }

        catch (Exception ex)
        {
            _statusMessage = $"Failed to load programs: {ex.Message}";
            _isError = true;
            _programs.Clear();
        }

        finally
        {
            _isLoading = false;
        }
    }

    private async Task SelectProgramAsync(ProgramEntity program)
    {
        _selectedProgram = program;
        _activeProgramTab = "tab-health";
        _canManageSelectedProgramProjects = _isTenantAdmin || CurrentUser.IsRootAdmin || _programAdminIds.Contains(program.Id);

        await LoadProgramHealthAsync();

        await LoadProgramProjectsAsync();

        await LoadProgramCalendarAsync();

        await LoadProgramRaidAsync();
    }

    private Guid ResolveSelectedTenantGuid()
    {
        if (Guid.TryParse(_selectedTenantId, out var directGuid) && directGuid != Guid.Empty)
        {
            return directGuid;
        }

        if (_selectedProgram != null && _selectedProgram.TenantId != Guid.Empty)
        {
            return _selectedProgram.TenantId;
        }

        if (TenantContext.HasActiveTenant && TenantContext.TenantId != Guid.Empty)
        {
            return TenantContext.TenantId;
        }

        return Guid.Empty;
    }

    private async Task LoadProgramCalendarAsync()
    {
        if (_selectedProgram == null || string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        _isLoadingCalendar = true;

        try
        {
            var start = DateTime.UtcNow.Date.AddMonths(-2);
            var end = DateTime.UtcNow.Date.AddMonths(10);
            var tenantGuid = _selectedProgram.TenantId != Guid.Empty ? _selectedProgram.TenantId : ResolveSelectedTenantGuid();
            _programCalendarItems = await CalendarService.GetProgramCalendarAsync(tenantGuid, _selectedProgram.Id, start, end);
        }

        catch (Exception ex)
        {
            _statusMessage = $"Failed to load program calendar: {ex.Message}";
            _isError = true;
        }

        finally
        {
            _isLoadingCalendar = false;
        }
    }

    private async Task LoadProgramRaidAsync()
    {
        if (_selectedProgram == null || string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        var tenantGuid = Guid.TryParse(_selectedTenantId, out var g) ? g : Guid.Empty;

        try
        {
            _programRisks = (await ProgramService.GetProgramRisksAsync(_selectedProgram.Id)).ToList();
            _programAssumptions = (await ProgramService.GetProgramAssumptionsAsync(_selectedProgram.Id)).ToList();
            _programIssues = (await ProgramService.GetProgramIssuesAsync(_selectedProgram.Id)).ToList();
            _programBlockers = (await ProgramService.GetProgramBlockersAsync(_selectedProgram.Id)).ToList();
        }

        catch (Exception ex)
        {
            _statusMessage = $"Failed to load program RAID items: {ex.Message}";
            _isError = true;
        }
    }

    private void OpenAddProgramCalendarModal(DateTime date)
    {
        _selectedCalendarDate = date;
        _statusMessage = $"Schedule event for {date:yyyy-MM-dd}.";
        _isError = false;
    }

    private async Task LoadProgramHealthAsync()
    {
        if (_selectedProgram == null) return;

        _isLoadingHealth = true;

        try
        {
            _healthSummary = await ProgramService.GetProgramHealthAsync(_selectedProgram.Id);
        }

        catch (Exception ex)
        {
            _statusMessage = $"Failed to calculate program health: {ex.Message}";
            _isError = true;
        }

        finally
        {
            _isLoadingHealth = false;
        }
    }

    private async Task LoadProgramProjectsAsync()
    {
        if (_selectedProgram == null) return;

        _isLoadingProjects = true;

        try
        {
            var projects = await ProgramService.GetProgramProjectsAsync(_selectedProgram.Id);
            _programProjects = projects.ToList();
        }

        catch (Exception ex)
        {
            _statusMessage = $"Failed to load program projects: {ex.Message}";
            _isError = true;
        }

        finally
        {
            _isLoadingProjects = false;
        }
    }

    private void OpenCreateProgramModal()
    {
        _editingProgramId = null;
        _programFormCode = string.Empty;
        _programFormName = string.Empty;
        _programFormDescription = string.Empty;
        _programFormStartDate = DateTime.UtcNow.Date;
        _programFormTargetEndDate = DateTime.UtcNow.Date.AddMonths(12);
        _programFormStatus = ProgramStatusEnum.Active;
        _showProgramModal = true;

        UnsavedChanges.RegisterChange("Settings > Programs", "Creating new program");
    }

    private void OpenEditProgramModal(ProgramEntity program)
    {
        _editingProgramId = program.Id;
        _programFormCode = program.Code;
        _programFormName = program.Name;
        _programFormDescription = program.Description;
        _programFormStartDate = program.StartDate;
        _programFormTargetEndDate = program.EndDate ?? program.DueDate;
        _programFormStatus = program.Status;
        _editingProgramVersion = program.Version;
        _showProgramModal = true;

        UnsavedChanges.RegisterChange("Settings > Programs", $"Editing program '{program.Name}'");
    }

    private void CloseProgramModal()
    {
        _showProgramModal = false;

        UnsavedChanges.ClearChange("Settings > Programs");
    }

    private async Task SaveProgramAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId) || !IsProgramFormValid) return;

        if (_editingProgramId.HasValue)
        {
            var updateResult = await ProgramService.UpdateProgramAsync(

                _editingProgramId.Value,

                _programFormName,

                _programFormCode,

                _programFormDescription,

                _programFormStartDate,

                _programFormTargetEndDate,

                _programFormStatus,

                isActive: true,

                version: _editingProgramVersion);

            if (updateResult.Succeeded && updateResult.Data is not null)
            {
                _statusMessage = $"Program '{updateResult.Data.Name}' updated successfully.";
                _isError = false;

                CloseProgramModal();

                await LoadProgramsAsync();

                if (_selectedProgram?.Id == _editingProgramId)
                {
                    await SelectProgramAsync(updateResult.Data);
                }
            }

            else
            {
                _statusMessage = updateResult.Errors.FirstOrDefault() ?? "Failed to update program.";
                _isError = true;
            }
        }

        else
        {
            var createResult = await ProgramService.CreateProgramAsync(

                _selectedTenantId,

                _programFormName,

                _programFormCode,

                _programFormDescription,

                _programFormStartDate,

                _programFormTargetEndDate,

                _programFormStatus);

            if (createResult.Succeeded && createResult.Data is not null)
            {
                _statusMessage = $"Program '{createResult.Data.Name}' created successfully.";
                _isError = false;

                CloseProgramModal();

                await LoadProgramsAsync();

                await SelectProgramAsync(createResult.Data);
            }

            else
            {
                _statusMessage = createResult.Errors.FirstOrDefault() ?? "Failed to create program.";
                _isError = true;
            }
        }
    }

    private void OpenAddProjectModal()
    {
        _projName = string.Empty;
        _projShortCode = string.Empty;
        _projLocation = string.Empty;
        _projStatus = ProjectStatusEnum.New;
        _projStartDate = DateTime.UtcNow.Date;
        _projEndDate = DateTime.UtcNow.Date.AddMonths(6);
        _projScopeSummary = string.Empty;
        _showAddProjectModal = true;

        UnsavedChanges.RegisterChange("Settings > Programs", "Adding project to program");
    }

    private void CloseProjectModal()
    {
        _showAddProjectModal = false;

        UnsavedChanges.ClearChange("Settings > Programs");
    }

    private async Task CreateProgramProjectAsync()
    {
        if (_selectedProgram == null || string.IsNullOrWhiteSpace(_selectedTenantId) || !IsProjectFormValid) return;

        var result = await TenantService.CreateTenantProjectAsync(

            _selectedTenantId,

            _projName,

            _projShortCode,

            _projLocation,

            _projStartDate,

            _projEndDate,

            _projScopeSummary,

            _projStatus,

            programId: _selectedProgram.Id);

        if (result.Succeeded && result.Data is not null)
        {
            _statusMessage = $"Project '{result.Data.Name}' added to program '{_selectedProgram.Name}'.";
            _isError = false;

            CloseProjectModal();

            await LoadProgramProjectsAsync();

            await LoadProgramHealthAsync();
        }

        else
        {
            _statusMessage = result.Errors.FirstOrDefault() ?? "Failed to add project.";
            _isError = true;
        }
    }

    private async Task HandleEditProgramFromWizardAsync()
    {
        if (_editingProgramId.HasValue)
        {
            var program = _programs.FirstOrDefault(p => p.Id == _editingProgramId.Value);

            if (program != null)
            {
                await OpenEditProgramWizard(program);
            }
        }
    }

    private async Task HandleEditProjectFromWizardAsync()
    {
        if (_editingProjectId.HasValue)
        {
            var project = _programProjects.FirstOrDefault(p => p.Id == _editingProjectId.Value)

                          ?? _programs.SelectMany(p => p.Projects).FirstOrDefault(p => p.Id == _editingProjectId.Value);

            if (project != null)
            {
                await OpenEditProjectWizard(project);
            }
        }
    }

    private void OpenCreateProgramWizard()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        _editingProgramId = null;
        _isProgramWizardEditMode = false;
        _isProgramWizardReadOnly = false;
        _programWizardTitle = "Create Enterprise Program";

        _programWizardInitialState = new BlazorEnterprise.Core.Dtos.CreateProgramWizardState
        {
            Status = ProgramStatusEnum.Planning,

            StartDate = DateTime.UtcNow.Date,

            EndDate = DateTime.UtcNow.Date.AddYears(1)
        };

        _showCreateProgramWizard = true;
    }

    private async Task OpenViewProgramWizard(ProgramEntity program)
    {
        _editingProgramId = program.Id;
        _isProgramWizardEditMode = false;
        _isProgramWizardReadOnly = true;
        _programWizardTitle = $"View Program: {program.Name}";
        var tenantGuid = program.TenantId != Guid.Empty ? program.TenantId : ResolveSelectedTenantGuid();
        var calendarEntries = await CalendarService.GetProgramCalendarEntitiesAsync(tenantGuid, program.Id, forEdit: false);

        var timelineRows = calendarEntries

            .Where(c => c.EntryType == CalendarEntryTypeEnum.Timeline)

            .OrderBy(c => c.StartDate)

            .Select(c => new BlazorEnterprise.Core.Dtos.ProgramTimelineRowDto
            {
                Id = c.Id,

                Title = c.Title,

                StartDate = c.StartDate,

                EndDate = c.EndDate,

                Status = c.Status.ToString(),

                Comments = c.Comments,

                Milestones = calendarEntries

                    .Where(m => m.EntryType == CalendarEntryTypeEnum.Milestone && m.ParentTimelineId == c.Id)

                    .OrderBy(m => m.StartDate)

                    .Select(m => new BlazorEnterprise.Core.Dtos.ProgramMilestoneDto
                    {
                        Id = m.Id,

                        ParentTimelineId = c.Id,

                        Title = m.Title,

                        StartDate = m.StartDate,

                        EndDate = m.EndDate,

                        OwnerEmails = m.OwnerEmails ?? string.Empty,

                        Status = m.Status.ToString(),

                        Comments = m.Comments
                    })

                    .ToList()
            })

            .ToList();

        var allMilestones = timelineRows.SelectMany(t => t.Milestones).ToList();

        _programWizardInitialState = new BlazorEnterprise.Core.Dtos.CreateProgramWizardState
        {
            ProgramId = program.Id,

            Code = program.Code,

            Name = program.Name,

            Description = program.Description ?? string.Empty,

            Status = program.Status,

            StartDate = program.StartDate,

            EndDate = program.EndDate ?? program.DueDate,

            AdminUsers = program.UserRoles

                .Where(r => !r.IsDeleted && r.Role == ProgramRoleEnum.Admin)

                .Select(r => new BlazorEnterprise.Core.Dtos.ProgramAdminUserDto
                {
                    UserId = r.UserEntityId,

                    DisplayName = r.User != null ? (string.IsNullOrWhiteSpace(r.User.FullName) ? r.User.Email : r.User.FullName) : r.UserEntityId.ToString(),

                    Email = r.User?.Email ?? string.Empty
                })

                .ToList(),

            TimelineRows = timelineRows,

            Milestones = allMilestones
        };

        _showCreateProgramWizard = true;
    }

    private async Task OpenEditProgramWizard(ProgramEntity program)
    {
        _editingProgramId = program.Id;
        var progResult = await ProgramService.GetProgramByIdAsync(program.Id, forEdit: true);

        if (!progResult.Succeeded || progResult.Data == null)
        {
            _statusMessage = progResult.Errors.FirstOrDefault() ?? "Unable to initialize edit mode. The program could not be re-queried or was modified by another transaction.";
            _isError = true;

            return;
        }

        var trackedProgram = progResult.Data;
        _isProgramWizardEditMode = true;
        _isProgramWizardReadOnly = false;
        _programWizardTitle = $"Edit Program: {trackedProgram.Name}";
        var tenantGuid = trackedProgram.TenantId != Guid.Empty ? trackedProgram.TenantId : ResolveSelectedTenantGuid();
        var calendarEntries = await CalendarService.GetProgramCalendarEntitiesAsync(tenantGuid, trackedProgram.Id, forEdit: true);

        var timelineRows = calendarEntries

            .Where(c => c.EntryType == CalendarEntryTypeEnum.Timeline)

            .OrderBy(c => c.StartDate)

            .Select(c => new BlazorEnterprise.Core.Dtos.ProgramTimelineRowDto
            {
                Id = c.Id,

                Title = c.Title,

                StartDate = c.StartDate,

                EndDate = c.EndDate,

                Status = c.Status.ToString(),

                Comments = c.Comments,

                Milestones = calendarEntries

                    .Where(m => m.EntryType == CalendarEntryTypeEnum.Milestone && m.ParentTimelineId == c.Id)

                    .OrderBy(m => m.StartDate)

                    .Select(m => new BlazorEnterprise.Core.Dtos.ProgramMilestoneDto
                    {
                        Id = m.Id,

                        ParentTimelineId = c.Id,

                        Title = m.Title,

                        StartDate = m.StartDate,

                        EndDate = m.EndDate,

                        OwnerEmails = m.OwnerEmails ?? string.Empty,

                        Status = m.Status.ToString(),

                        Comments = m.Comments
                    })

                    .ToList()
            })

            .ToList();

        var allMilestones = timelineRows.SelectMany(t => t.Milestones).ToList();

        _programWizardInitialState = new BlazorEnterprise.Core.Dtos.CreateProgramWizardState
        {
            ProgramId = trackedProgram.Id,

            Code = trackedProgram.Code,

            Name = trackedProgram.Name,

            Description = trackedProgram.Description ?? string.Empty,

            Status = trackedProgram.Status,

            StartDate = trackedProgram.StartDate,

            EndDate = trackedProgram.EndDate ?? trackedProgram.DueDate,

            AdminUsers = trackedProgram.UserRoles

                .Where(r => !r.IsDeleted && r.Role == ProgramRoleEnum.Admin)

                .Select(r => new BlazorEnterprise.Core.Dtos.ProgramAdminUserDto
                {
                    UserId = r.UserEntityId,

                    DisplayName = r.User != null ? (string.IsNullOrWhiteSpace(r.User.FullName) ? r.User.Email : r.User.FullName) : r.UserEntityId.ToString(),

                    Email = r.User?.Email ?? string.Empty
                })

                .ToList(),

            TimelineRows = timelineRows,

            Milestones = allMilestones
        };

        _showCreateProgramWizard = true;
    }

    private async Task<string?> OnProgramWizardSavedAsync(BlazorEnterprise.Core.Dtos.CreateProgramWizardState state)
    {
        CorrelationContext.Renew();

        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return "No active tenant selected.";

        _isLoading = true;

        StateHasChanged();

        try
        {
            Guid targetProgramId;

            string programName;

            if (_isProgramWizardEditMode && _editingProgramId.HasValue)
            {
                var updateResult = await ProgramService.UpdateProgramAsync(

                    _editingProgramId.Value,

                    state.Name,

                    state.Code,

                    state.Description,

                    state.StartDate,

                    state.EndDate,

                    state.Status,

                    isActive: true);

                if (!updateResult.Succeeded || updateResult.Data is null)
                {
                    var err = updateResult.Errors.FirstOrDefault() ?? "Failed to update program.";
                    _statusMessage = err;
                    _isError = true;

                    return err;
                }

                targetProgramId = _editingProgramId.Value;

                programName = updateResult.Data.Name;
            }

            else
            {
                var createResult = await ProgramService.CreateProgramAsync(

                    _selectedTenantId,

                    state.Name,

                    state.Code,

                    state.Description,

                    state.StartDate,

                    state.EndDate,

                    state.Status);

                if (!createResult.Succeeded || createResult.Data is null)
                {
                    var err = createResult.Errors.FirstOrDefault() ?? "Failed to create program.";
                    _statusMessage = err;
                    _isError = true;

                    return err;
                }

                targetProgramId = createResult.Data.Id;

                programName = createResult.Data.Name;
            }

            // Sync Program Admins

            if (state.AdminUsers != null)
            {
                var adminUserIds = state.AdminUsers.Select(a => a.UserId).ToList();
                var adminSyncResult = await ProgramService.SyncProgramAdminsAsync(targetProgramId, adminUserIds);

                if (!adminSyncResult.Succeeded)
                {
                    var err = $"Failed to synchronize program administrators: {string.Join("; ", adminSyncResult.Errors)}";
                    _statusMessage = err;
                    _isError = true;

                    return err;
                }
            }

            // Persist Timeline rows and Milestones via shared ICalendarService
            var tenantGuid = ResolveSelectedTenantGuid();
            if (tenantGuid != Guid.Empty && state.TimelineRows != null)
            {
                var saveScheduleResult = await CalendarService.SaveProgramCalendarScheduleAsync(
                    tenantId: tenantGuid,
                    programId: targetProgramId,
                    timelineRows: state.TimelineRows,
                    isBatchDraft: false);

                if (!saveScheduleResult.Succeeded)
                {
                    var err = saveScheduleResult.Errors.FirstOrDefault() ?? "Failed to save program schedule.";
                    _statusMessage = err;
                    _isError = true;
                    return err;
                }
            }

            _statusMessage = _isProgramWizardEditMode

                ? $"Program '{programName}' updated successfully."

                : $"Program '{programName}' created successfully.";

            _isError = false;

            await LoadProgramsAsync();

            var targetProg = _programs.FirstOrDefault(p => p.Id == targetProgramId);

            if (targetProg != null)
            {
                await SelectProgramAsync(targetProg);
            }

            return null;
        }

        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var err = "Concurrency Conflict: This program was modified by another user while your edit was in progress. Your changes were not applied to avoid overwriting newer data. Please refresh and review.";
            return err;
        }

        catch (Exception ex)
        {
            var err = $"An error occurred while saving the program: {ex.Message}";
            return err;
        }

        finally
        {
            _isLoading = false;

            StateHasChanged();
        }
    }

    private void ToggleProgramExpanded(Guid programId)
    {
        if (_expandedProgramIds.Contains(programId))
        {
            _expandedProgramIds.Remove(programId);
        }

        else
        {
            _expandedProgramIds.Add(programId);
        }
    }

    private static BadgeColor GetProjectStatusBadgeColor(ProjectStatusEnum status) => status switch
    {
        ProjectStatusEnum.Active => BadgeColor.Success,

        ProjectStatusEnum.OnHold => BadgeColor.Warning,

        _ => BadgeColor.Neutral
    };

    private void OpenCreateProjectWizard(ProgramEntity program)
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        _editingProjectId = null;
        _isWizardEditMode = false;
        _isWizardReadOnly = false;
        _wizardTitle = $"Create Strategic Project for {program.Name}";

        _wizardInitialState = new BlazorEnterprise.Core.Dtos.CreateProjectWizardState
        {
            ProgramId = program.Id
        };

        _showCreateProjectWizard = true;
    }

    private async Task OpenViewProjectWizard(ProjectEntity project)
    {
        _editingProjectId = project.Id;
        _isWizardEditMode = false;
        _isWizardReadOnly = true;
        _wizardTitle = $"View Project: {project.Name}";
        var tenantGuid = project.TenantId != Guid.Empty ? project.TenantId : ResolveSelectedTenantGuid();
        var calendarEntries = await CalendarService.GetProjectCalendarEntitiesAsync(tenantGuid, project.Id, forEdit: false);

        var phases = calendarEntries

            .Where(c => c.EntryType == CalendarEntryTypeEnum.Timeline)

            .OrderBy(c => c.StartDate)

            .Select(c => new BlazorEnterprise.Core.Dtos.ProjectPhaseDto
            {
                Id = c.Id,

                Title = c.Title,

                StartDate = c.StartDate,

                EndDate = c.EndDate,

                Status = c.Status.ToString(),

                Comments = c.Comments,

                Milestones = calendarEntries

                    .Where(m => m.EntryType == CalendarEntryTypeEnum.Milestone && m.ParentTimelineId == c.Id)

                    .OrderBy(m => m.StartDate)

                    .Select(m => new BlazorEnterprise.Core.Dtos.ProjectMilestoneDto
                    {
                        Id = m.Id,

                        ParentTimelineId = c.Id,

                        Title = m.Title,

                        StartDate = m.StartDate,

                        EndDate = m.EndDate,

                        OwnerEmails = m.OwnerEmails ?? string.Empty,

                        Status = m.Status.ToString(),

                        Comments = m.Comments
                    })

                    .ToList()
            })

            .ToList();

        var allMilestones = phases.SelectMany(p => p.Milestones).ToList();

        var roster = project.UserRoles

            .Where(r => !r.IsDeleted)

            .Select(r => new BlazorEnterprise.Core.Dtos.ProjectRosterMemberDto
            {
                Id = r.Id,

                FullName = r.User != null ? (string.IsNullOrWhiteSpace(r.User.FullName) ? r.User.Email : r.User.FullName) : r.UserEntityId.ToString(),

                Email = r.User?.Email ?? string.Empty,

                Role = r.Role.ToString(),

                StartDate = null,

                EndDate = null
            })

            .ToList();

        _wizardInitialState = new BlazorEnterprise.Core.Dtos.CreateProjectWizardState
        {
            ProgramId = project.ProgramId,

            Name = project.Name,

            ShortCode = project.ShortCode ?? string.Empty,

            ScopeSummary = project.ScopeSummary ?? project.Description ?? string.Empty,

            StartDate = project.StartDate,

            EndDate = project.EndDate ?? project.DueDate,

            Roster = roster,

            Phases = phases,

            Milestones = allMilestones
        };

        _showCreateProjectWizard = true;
    }

    private async Task OpenEditProjectWizard(ProjectEntity project)
    {
        _editingProjectId = project.Id;
        var tenantGuid = project.TenantId != Guid.Empty ? project.TenantId : ResolveSelectedTenantGuid();
        var projects = await TenantService.GetProjectsByTenantIdAsync(tenantGuid);
        var trackedProject = projects.FirstOrDefault(p => p.Id == project.Id);

        if (trackedProject == null)
        {
            _statusMessage = "Unable to initialize edit mode. The project could not be re-queried or was deleted by another transaction.";
            _isError = true;

            return;
        }

        _isWizardEditMode = true;
        _isWizardReadOnly = false;
        _wizardTitle = $"Edit Project: {trackedProject.Name}";
        var calendarEntries = await CalendarService.GetProjectCalendarEntitiesAsync(tenantGuid, trackedProject.Id, forEdit: true);

        var phases = calendarEntries

            .Where(c => c.EntryType == CalendarEntryTypeEnum.Timeline)

            .OrderBy(c => c.StartDate)

            .Select(c => new BlazorEnterprise.Core.Dtos.ProjectPhaseDto
            {
                Id = c.Id,

                Title = c.Title,

                StartDate = c.StartDate,

                EndDate = c.EndDate,

                Status = c.Status.ToString(),

                Comments = c.Comments,

                Milestones = calendarEntries

                    .Where(m => m.EntryType == CalendarEntryTypeEnum.Milestone && m.ParentTimelineId == c.Id)

                    .OrderBy(m => m.StartDate)

                    .Select(m => new BlazorEnterprise.Core.Dtos.ProjectMilestoneDto
                    {
                        Id = m.Id,

                        ParentTimelineId = c.Id,

                        Title = m.Title,

                        StartDate = m.StartDate,

                        EndDate = m.EndDate,

                        OwnerEmails = m.OwnerEmails ?? string.Empty,

                        Status = m.Status.ToString(),

                        Comments = m.Comments
                    })

                    .ToList()
            })

            .ToList();

        var allMilestones = phases.SelectMany(p => p.Milestones).ToList();

        var roster = project.UserRoles

            .Where(r => !r.IsDeleted)

            .Select(r => new BlazorEnterprise.Core.Dtos.ProjectRosterMemberDto
            {
                Id = r.Id,

                FullName = r.User != null ? (string.IsNullOrWhiteSpace(r.User.FullName) ? r.User.Email : r.User.FullName) : r.UserEntityId.ToString(),

                Email = r.User?.Email ?? string.Empty,

                Role = r.Role.ToString(),

                StartDate = null,

                EndDate = null
            })

            .ToList();

        _wizardInitialState = new BlazorEnterprise.Core.Dtos.CreateProjectWizardState
        {
            ProgramId = trackedProject.ProgramId,

            Name = trackedProject.Name,

            ShortCode = trackedProject.ShortCode ?? string.Empty,

            ScopeSummary = trackedProject.ScopeSummary ?? trackedProject.Description ?? string.Empty,

            StartDate = trackedProject.StartDate,

            EndDate = trackedProject.EndDate ?? trackedProject.DueDate,

            Roster = roster,

            Phases = phases,

            Milestones = allMilestones
        };

        _showCreateProjectWizard = true;
    }

    private async Task<string?> OnProjectWizardSavedAsync(BlazorEnterprise.Core.Dtos.CreateProjectWizardState state)
    {
        CorrelationContext.Renew();

        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return "No active tenant selected.";

        _isLoading = true;

        StateHasChanged();

        try
        {
            Guid targetProjectId;

            string projectName;

            string? projectShortCode;

            if (_isWizardEditMode && _editingProjectId.HasValue)
            {
                var existingProject = _programs.SelectMany(p => p.Projects).FirstOrDefault(pr => pr.Id == _editingProjectId.Value);
                var status = existingProject?.Status ?? ProjectStatusEnum.New;
                var location = existingProject?.Location;

                var result = await TenantService.UpdateTenantProjectAsync(

                    tenantSlug: _selectedTenantId,

                    projectId: _editingProjectId.Value,

                    name: state.Name,

                    shortCode: state.ShortCode,

                    location: location,

                    startDate: state.StartDate,

                    endDate: state.EndDate,

                    scopeSummary: state.ScopeSummary,

                    status: status,

                    programId: state.ProgramId);

                if (!result.Succeeded || result.Data is null)
                {
                    var err = result.Errors.FirstOrDefault() ?? "Failed to update project.";
                    _statusMessage = err;
                    _isError = true;

                    return err;
                }

                targetProjectId = _editingProjectId.Value;

                projectName = result.Data.Name;

                projectShortCode = result.Data.ShortCode;
            }

            else
            {
                var result = await TenantService.CreateTenantProjectAsync(

                    tenantSlug: _selectedTenantId,

                    name: state.Name,

                    shortCode: state.ShortCode,

                    location: null,

                    startDate: state.StartDate,

                    endDate: state.EndDate,

                    scopeSummary: state.ScopeSummary,

                    status: ProjectStatusEnum.New,

                    programId: state.ProgramId);

                if (!result.Succeeded || result.Data is null)
                {
                    var err = result.Errors.FirstOrDefault() ?? "Failed to create project.";
                    _statusMessage = err;
                    _isError = true;

                    return err;
                }

                targetProjectId = result.Data.Id;

                projectName = result.Data.Name;

                projectShortCode = result.Data.ShortCode;
            }

            // Sync Project Roster Members

            if (state.Roster != null)
            {
                var rosterSyncResult = await TenantService.SyncProjectRosterAsync(targetProjectId, state.Roster);

                if (!rosterSyncResult.Succeeded)
                {
                    var err = $"Failed to synchronize project team roster: {string.Join("; ", rosterSyncResult.Errors)}";
                    _statusMessage = err;
                    _isError = true;

                    return err;
                }
            }

            // Persist Project timeline phases and milestones via shared ICalendarService
            var tenantGuid = ResolveSelectedTenantGuid();
            if (tenantGuid != Guid.Empty && state.Phases != null)
            {
                var saveScheduleResult = await CalendarService.SaveProjectCalendarScheduleAsync(
                    tenantId: tenantGuid,
                    projectId: targetProjectId,
                    programId: state.ProgramId,
                    phases: state.Phases,
                    isBatchDraft: false);

                if (!saveScheduleResult.Succeeded)
                {
                    var err = saveScheduleResult.Errors.FirstOrDefault() ?? "Failed to save project schedule.";
                    _statusMessage = err;
                    _isError = true;
                    return err;
                }
            }

            _statusMessage = _isWizardEditMode

                ? $"Project '{projectName}' updated successfully."

                : $"Project '{projectName}' ({projectShortCode}) created successfully.";

            _isError = false;

            await LoadProgramsAsync();

            if (state.ProgramId.HasValue)
            {
                _expandedProgramIds.Add(state.ProgramId.Value);
            }

            return null;
        }

        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            var err = "Concurrency Conflict: This project was modified by another user while your edit was in progress. Your changes were not applied to avoid overwriting newer data. Please refresh and review.";
            return err;
        }

        catch (Exception ex)
        {
            var err = $"An error occurred while saving the project: {ex.Message}";
            return err;
        }

        finally
        {
            _isLoading = false;

            StateHasChanged();
        }
    }

    public void Dispose()
    {
        UnsavedChanges.ClearChange("Settings > Programs");
    }
}
