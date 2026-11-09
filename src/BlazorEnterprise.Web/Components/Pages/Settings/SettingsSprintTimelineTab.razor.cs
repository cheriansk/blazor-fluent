using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsSprintTimelineTab : ComponentBase, IDisposable
{
    [Inject] private ISprintPlanningService SprintService { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;

    private List<SprintDto> _sprints = new();
    private string _filterStatus = string.Empty;
    private string _errorMessage = string.Empty;

    // Modal state
    private bool _showModal;
    private bool _isEditing;
    private Guid _editingSprintId;
    private string _modalSprintName = string.Empty;
    private int _modalSprintNumber = 1;
    private DateTime _modalStartDate = DateTime.UtcNow.Date;
    private DateTime _modalEndDate = DateTime.UtcNow.Date.AddDays(14);
    private string? _modalComments;

    private IEnumerable<SprintDto> FilteredSprints => _sprints
        .Where(s => string.IsNullOrWhiteSpace(_filterStatus) || s.Status.ToString() == _filterStatus);

    protected override async Task OnInitializedAsync()
    {
        ProjectContext.OnChange += HandleProjectChanged;
        await LoadSprintsAsync();
    }

    private async void HandleProjectChanged()
    {
        await InvokeAsync(async () =>
        {
            await LoadSprintsAsync();
            StateHasChanged();
        });
    }

    private async Task LoadSprintsAsync()
    {
        if (!ProjectContext.HasProject) return;
        _errorMessage = string.Empty;
        _sprints = await SprintService.GetProjectSprintsAsync(ProjectContext.ProjectId!.Value);
    }

    private void OpenCreateModal()
    {
        _isEditing = false;
        _editingSprintId = Guid.Empty;
        var nextNum = _sprints.Count > 0 ? _sprints.Max(s => s.SprintNumber) + 1 : 1;
        _modalSprintNumber = nextNum;
        _modalSprintName = $"Sprint {nextNum}";

        if (_sprints.Count > 0)
        {
            var latestSprint = _sprints.OrderByDescending(s => s.EndDate).First();
            _modalStartDate = latestSprint.EndDate;
            _modalEndDate = latestSprint.EndDate.AddDays(14);
        }
        else
        {
            _modalStartDate = DateTime.UtcNow.Date;
            _modalEndDate = DateTime.UtcNow.Date.AddDays(14);
        }

        _modalComments = string.Empty;
        _errorMessage = string.Empty;
        _showModal = true;
    }

    private void OpenEditModal(SprintDto sprint)
    {
        _isEditing = true;
        _editingSprintId = sprint.Id;
        _modalSprintNumber = sprint.SprintNumber;
        _modalSprintName = sprint.SprintName;
        _modalStartDate = sprint.StartDate;
        _modalEndDate = sprint.EndDate;
        _modalComments = sprint.Comments;
        _errorMessage = string.Empty;
        _showModal = true;
    }

    private async Task SaveSprint()
    {
        if (!ProjectContext.HasProject) return;
        _errorMessage = string.Empty;

        if (_modalEndDate <= _modalStartDate)
        {
            _errorMessage = "Sprint end date must be after the start date.";
            return;
        }

        // Pre-validate non-overlapping dates: S1 < E2 and S2 < E1
        var overlap = _sprints.FirstOrDefault(s =>
            (!_isEditing || s.Id != _editingSprintId) &&
            _modalStartDate < s.EndDate && s.StartDate < _modalEndDate);

        if (overlap != null)
        {
            _errorMessage = $"Sprint dates overlap with '{overlap.SprintName}' ({overlap.StartDate:MMM dd, yyyy} - {overlap.EndDate:MMM dd, yyyy}). The end date of one sprint can be the start date of another, but intervals cannot overlap.";
            return;
        }

        try
        {
            if (_isEditing)
            {
                await SprintService.UpdateSprintAsync(
                    _editingSprintId,
                    _modalSprintName,
                    _modalStartDate,
                    _modalEndDate,
                    _modalComments);
            }
            else
            {
                await SprintService.CreateSprintAsync(
                    ProjectContext.ProjectId!.Value,
                    _modalSprintName,
                    _modalSprintNumber,
                    _modalStartDate,
                    _modalEndDate,
                    _modalComments);
            }
            _showModal = false;
            await LoadSprintsAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task DeleteSprint(Guid sprintId)
    {
        _errorMessage = string.Empty;
        try
        {
            await SprintService.DeleteSprintAsync(sprintId);
            await LoadSprintsAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    public void Dispose()
    {
        ProjectContext.OnChange -= HandleProjectChanged;
    }

    private void HandleStartDateChanged(ChangeEventArgs e)
    {
        if (DateTime.TryParse(e.Value?.ToString(), out var d))
        {
            _modalStartDate = d;
        }
    }

    private void HandleEndDateChanged(ChangeEventArgs e)
    {
        if (DateTime.TryParse(e.Value?.ToString(), out var d))
        {
            _modalEndDate = d;
        }
    }
}
