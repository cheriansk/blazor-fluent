using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Shared.DataListTypes;
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

    private async Task LoadProgramCalendarAsync()
    {
        if (_selectedProgram == null || string.IsNullOrWhiteSpace(_selectedTenantId)) return;
        _isLoadingCalendar = true;
        try
        {
            var start = DateTime.UtcNow.Date.AddMonths(-2);
            var end = DateTime.UtcNow.Date.AddMonths(10);
            var tenantGuid = Guid.TryParse(_selectedTenantId, out var g) ? g : Guid.Empty;
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
                isActive: true);

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

    public void Dispose()
    {
        UnsavedChanges.ClearChange("Settings > Programs");
    }
}
