using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Jobs;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsJobsTab : ComponentBase, IDisposable
{
    [Inject] private IJobManagerService JobManagerService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IRootAdminService RootAdmin { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IDateTimeProvider DateTimeProvider { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;

    public record TenantFilterOption(string Id, string Name);
    public record JobTriggerOption(string Id, string Name);

    private readonly List<JobTriggerOption> _availableJobs = new()
    {
        new("TaskCadenceAlertJob", "Task Cadence Alert Job"),
        new("DailyTaskSummaryJob", "Daily Task Summary Job"),
        new("AuditPurgeJob", "Forensic Audit Purge Job")
    };
    private string _selectedJobToRun = "TaskCadenceAlertJob";

    private List<TenantFilterOption> _availableTenants = new();
    private string? _selectedTenantId;
    private string _selectedTenantName = string.Empty;

    private List<JobExecutionEntity> _executions = new();
    private string _jobSearchText = string.Empty;
    private JobExecutionEntity? _selectedErrorExecution;
    private bool _isLoading;
    private string? _statusMessage;
    private bool _isError;

    protected override async Task OnInitializedAsync()
    {
        TimeZoneService.OnChange += HandleTimeZoneChanged;
        await LoadAvailableTenantsAsync();
    }

    private async Task LoadAvailableTenantsAsync()
    {
        var isRootAdmin = CurrentUser.IsRootAdmin;
        if (isRootAdmin)
        {
            var dbTenants = (await TenantService.GetAllTenantsAsync())
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(
                    t.Slug,
                    t.Slug == RootAdmin.RootTenantSlug ? $"{t.Name} (Default Root)" : t.Name))
                .ToList();

            _availableTenants = dbTenants;
        }
        else
        {
            _availableTenants = TenantContext.AllowedTenants
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(t.Id.ToString(), t.Name))
                .ToList();
        }

        if (_availableTenants.Count == 1)
        {
            await OnTenantSelected(_availableTenants[0].Id);
        }
    }

    private async Task OnTenantSelected(string? tenantId)
    {
        _selectedTenantId = tenantId;
        _selectedTenantName = _availableTenants.FirstOrDefault(t => t.Id == tenantId)?.Name ?? tenantId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_selectedTenantId))
        {
            await LoadExecutionsAsync();
        }
        else
        {
            _executions.Clear();
            _selectedErrorExecution = null;
        }
    }

    private void ClearTenantFilter()
    {
        _selectedTenantId = null;
        _selectedTenantName = string.Empty;
        _executions.Clear();
        _selectedErrorExecution = null;
    }

    private async Task LoadExecutionsAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId)) return;

        _isLoading = true;
        try
        {
            var results = await JobManagerService.GetRecentExecutionsAsync(limit: 200);
            if (Guid.TryParse(_selectedTenantId, out var selectedTenantGuid))
            {
                _executions = results.Where(e => e.TenantId == selectedTenantGuid).ToList();
            }
            else
            {
                _executions = results.Where(e => e.TenantId == BlazorEnterprise.Core.Utilities.SystemIdentityUtility.Current.RootTenantId || e.TenantId == Guid.Empty).ToList();
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task TriggerSelectedJobAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedTenantId) || string.IsNullOrWhiteSpace(_selectedJobToRun)) return;

        var result = await JobManagerService.TriggerJobAsync(_selectedJobToRun, _selectedTenantId);
        if (result.Succeeded)
        {
            _statusMessage = result.Message ?? $"Job '{_selectedJobToRun}' enqueued successfully. Processing in background.";
            _isError = false;
            await Task.Delay(300);
            await LoadExecutionsAsync();
        }
        else
        {
            _statusMessage = $"Failed to trigger job: {result.Error ?? "Unknown error"}";
            _isError = true;
        }
    }

    private async Task RetryJobAsync(Guid executionId)
    {
        _selectedErrorExecution = null;
        var result = await JobManagerService.RetryJobAsync(executionId);
        if (result.Succeeded)
        {
            _statusMessage = result.Message ?? "Manual retry enqueued successfully. Processing in background.";
            _isError = false;
            await Task.Delay(300);
            await LoadExecutionsAsync();
        }
        else
        {
            _statusMessage = $"Failed to initiate retry: {result.Error ?? "Unknown error"}";
            _isError = true;
        }
    }

    private IQueryable<JobExecutionEntity> FilteredExecutions
    {
        get
        {
            var query = _executions.AsQueryable();
            if (!string.IsNullOrWhiteSpace(_jobSearchText))
            {
                var term = _jobSearchText.Trim();
                query = query.Where(e =>
                    (e.JobName != null && e.JobName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    e.TriggerSource.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (e.CorrelationId != null && e.CorrelationId.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }
            return query;
        }
    }

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
    }
}
