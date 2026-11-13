using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsRateCardTab : ComponentBase, IDisposable
{
    [Inject] private IResourceManagementService ResourceService { get; set; } = default!;
    [Inject] private IRevisionService RevisionService { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private ILogger<SettingsRateCardTab> Logger { get; set; } = default!;

    private List<ProjectResourceDto> _resources = new();
    private List<RevisionSummaryDto> _revisions = new();
    private Guid _selectedRevisionId;
    private bool _isReadOnly = true;
    private string _searchTerm = string.Empty;

    private ProjectResourceRoleEnum _bulkRole = ProjectResourceRoleEnum.Developer;
    private decimal _bulkRate = 100m;

    private IEnumerable<ProjectResourceDto> FilteredResources => _resources
        .Where(r => string.IsNullOrWhiteSpace(_searchTerm) ||
                    r.UserName.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    r.UserEmail.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        ProjectContext.OnChange += HandleProjectChanged;
        await LoadRatesAsync();
    }

    private async void HandleProjectChanged()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                await LoadRatesAsync();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh rate card tab on project change.");
        }
    }

    private async Task LoadRatesAsync()
    {
        if (!ProjectContext.HasProject) return;

        var projectId = ProjectContext.ProjectId!.Value;
        _revisions = await ResourceService.GetAllocationPlanHistoryAsync(projectId);

        var draft = _revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Draft);
        var active = _revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Active);

        if (_selectedRevisionId == Guid.Empty || !_revisions.Any(r => r.Id == _selectedRevisionId))
        {
            _selectedRevisionId = draft?.Id ?? active?.Id ?? Guid.Empty;
        }

        var header = _selectedRevisionId != Guid.Empty
            ? _revisions.FirstOrDefault(r => r.Id == _selectedRevisionId)
            : null;

        _isReadOnly = header == null || header.Status != RevisionStatusEnum.Draft;
        _resources = await ResourceService.GetRateCardAsync(
            projectId,
            _selectedRevisionId != Guid.Empty ? _selectedRevisionId : null);
    }

    private async Task OnRevisionSelected(ChangeEventArgs e)
    {
        if (Guid.TryParse(e.Value?.ToString(), out var revId))
        {
            _selectedRevisionId = revId;
            await LoadRatesAsync();
        }
    }

    private async Task UpdateHourlyRate(Guid resourceId, string? rateStr)
    {
        if (_isReadOnly || _selectedRevisionId == Guid.Empty) return;
        if (decimal.TryParse(rateStr, out var rate))
        {
            await ResourceService.SetResourceRateAsync(_selectedRevisionId, resourceId, rate);
            await LoadRatesAsync();
        }
    }

    private async Task ApplyBulkRate()
    {
        if (_isReadOnly || _selectedRevisionId == Guid.Empty || !ProjectContext.HasProject) return;
        await ResourceService.BulkSetRateByRoleAsync(
            _selectedRevisionId,
            ProjectContext.ProjectId!.Value,
            _bulkRole,
            _bulkRate);
        await LoadRatesAsync();
    }

    public void Dispose()
    {
        ProjectContext.OnChange -= HandleProjectChanged;
    }
}
