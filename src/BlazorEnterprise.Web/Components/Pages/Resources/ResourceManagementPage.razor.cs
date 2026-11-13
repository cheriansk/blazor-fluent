using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Extensions;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Resources;

public partial class ResourceManagementPage : ComponentBase, IDisposable
{
    [Inject] private IResourceManagementService ResourceService { get; set; } = default!;
    [Inject] private IRevisionService RevisionService { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private NavigationManager NavManager { get; set; } = default!;
    [Inject] private ILogger<ResourceManagementPage> Logger { get; set; } = default!;

    private ResourcePlanSummaryDto? _planSummary;
    private List<RevisionSummaryDto> _revisions = new();
    private Guid _selectedRevisionId;
    private bool _isReadOnly => _planSummary?.IsLocked ?? true;

    private string _filterName = string.Empty;
    private string _filterRole = string.Empty;

    private bool _showAddResourceModal;
    private List<UserEntity> _availableUsers = new();

    private IEnumerable<ResourceDailyAllocationDto> FilteredResources
    {
        get
        {
            if (_planSummary == null) return Enumerable.Empty<ResourceDailyAllocationDto>();

            var list = _planSummary.Resources.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(_filterName))
            {
                list = list.Where(r => r.ResourceName.Contains(_filterName, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(_filterRole) && _filterRole.TryToEnum<ProjectResourceRoleEnum>(out var roleVal))
            {
                list = list.Where(r => r.Role == roleVal);
            }
            return list;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        ProjectContext.OnChange += HandleProjectChanged;
        await LoadPageDataAsync();
    }

    private async void HandleProjectChanged()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                await LoadPageDataAsync();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh resource management page on project change.");
        }
    }

    private async Task LoadPageDataAsync()
    {
        if (!ProjectContext.HasProject) return;

        var projectId = ProjectContext.ProjectId!.Value;
        _revisions = await ResourceService.GetAllocationPlanHistoryAsync(projectId);

        if (_selectedRevisionId == Guid.Empty || !_revisions.Any(r => r.Id == _selectedRevisionId))
        {
            var active = _revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Active);
            var draft = _revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Draft);
            _selectedRevisionId = draft?.Id ?? active?.Id ?? Guid.Empty;
        }

        _planSummary = await ResourceService.GetResourcePlanSummaryAsync(
            projectId,
            _selectedRevisionId != Guid.Empty ? _selectedRevisionId : null);

        if (_planSummary != null && _selectedRevisionId == Guid.Empty)
        {
            _selectedRevisionId = _planSummary.RevisionHeaderId;
            _revisions = await ResourceService.GetAllocationPlanHistoryAsync(projectId);
        }
    }

    private async Task HandleRevisionChanged(Guid revisionId)
    {
        _selectedRevisionId = revisionId;
        if (ProjectContext.HasProject)
        {
            _planSummary = await ResourceService.GetResourcePlanSummaryAsync(ProjectContext.ProjectId!.Value, revisionId);
        }
    }

    private async Task HandleDataMutated()
    {
        await LoadPageDataAsync();
    }

    private async Task OpenAddResourceModal()
    {
        if (!ProjectContext.HasProject) return;
        _availableUsers = await ResourceService.GetAvailableProjectUsersAsync(ProjectContext.ProjectId!.Value);
        _showAddResourceModal = true;
    }

    private async Task HandleUsersSelected(List<(Guid UserId, ProjectResourceRoleEnum Role)> selections)
    {
        _showAddResourceModal = false;
        if (!ProjectContext.HasProject) return;

        foreach (var (userId, role) in selections)
        {
            await ResourceService.AddResourceAsync(ProjectContext.ProjectId!.Value, userId, role);
        }

        await LoadPageDataAsync();
    }

    private async Task RemoveResource(Guid projectResourceId)
    {
        await ResourceService.RemoveResourceAsync(projectResourceId);
        await LoadPageDataAsync();
    }

    private async Task UpdateResourceRole(Guid projectResourceId, string? roleStr)
    {
        if (roleStr.TryToEnum<ProjectResourceRoleEnum>(out var roleVal))
        {
            await ResourceService.UpdateResourceRoleAsync(projectResourceId, roleVal);
            await LoadPageDataAsync();
        }
    }

    private async Task SetCellAllocation(Guid projectResourceId, DateOnly date, string? valueStr)
    {
        if (_planSummary == null) return;
        if (int.TryParse(valueStr, out var pct))
        {
            pct = Math.Clamp(pct, 0, 100);
            await ResourceService.SetResourceAllocationAsync(_planSummary.RevisionHeaderId, projectResourceId, date, pct);
            await LoadPageDataAsync();
        }
    }

    private static string GetCellCssClass(int pct) => pct switch
    {
        0 => "cell-0",
        <= 50 => "cell-partial",
        < 100 => "cell-high",
        _ => "cell-full"
    };

    public void Dispose()
    {
        ProjectContext.OnChange -= HandleProjectChanged;
    }
}
