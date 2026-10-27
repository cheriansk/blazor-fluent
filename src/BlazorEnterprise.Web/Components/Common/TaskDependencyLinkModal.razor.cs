using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Common;

public partial class TaskDependencyLinkModal : ComponentBase
{
    [Inject] private IUserTaskService UserTaskService { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public Guid SourceTaskId { get; set; }
    [Parameter] public string SourceTaskTitle { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<TaskEntity> AvailableTargetTasks { get; set; } = [];
    [Parameter] public EventCallback OnLinkCreated { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private TaskDependencyTypeEnum _selectedLinkType = TaskDependencyTypeEnum.BlockedBy;
    private Guid _selectedTargetTaskId = Guid.Empty;
    private DateTime? _resolveDueDate = DateTime.UtcNow.AddDays(7);
    private DependencyCriticalityEnum _criticality = DependencyCriticalityEnum.High;
    private string _impactDetails = string.Empty;
    private string _watcherEmails = string.Empty;
    private bool _isSubmitting;
    private string? _modalError;

    private async Task Close()
    {
        _modalError = null;
        IsOpen = false;
        await OnClose.InvokeAsync();
    }

    private async Task SaveLinkAsync()
    {
        _modalError = null;

        if (_selectedTargetTaskId == Guid.Empty)
        {
            _modalError = "Please select a target prerequisite task.";
            return;
        }

        if (_selectedLinkType == TaskDependencyTypeEnum.BlockedBy)
        {
            if (string.IsNullOrWhiteSpace(_impactDetails))
            {
                _modalError = "Impact details describing consequences of unresolved blocker are required.";
                return;
            }

            if (!_resolveDueDate.HasValue)
            {
                _modalError = "Resolve SLA due date is mandatory.";
                return;
            }
        }

        _isSubmitting = true;
        try
        {
            if (_selectedLinkType == TaskDependencyTypeEnum.BlockedBy)
            {
                var result = await UserTaskService.CreateBlockedByLinkAsync(
                    SourceTaskId,
                    _selectedTargetTaskId,
                    _resolveDueDate!.Value,
                    _criticality,
                    _impactDetails,
                    _watcherEmails);

                if (result.IsSuccess)
                {
                    await OnLinkCreated.InvokeAsync();
                    await Close();
                }
                else
                {
                    _modalError = result.Error ?? "Failed to create blocker link.";
                }
            }
            else
            {
                // Relates To
                var result = await UserTaskService.AddTaskDependencyAsync(
                    Guid.Empty,
                    SourceTaskId,
                    _selectedTargetTaskId,
                    TaskDependencyTypeEnum.RelatesTo);

                if (result.IsSuccess)
                {
                    await OnLinkCreated.InvokeAsync();
                    await Close();
                }
                else
                {
                    _modalError = result.Error ?? "Failed to create relationship.";
                }
            }
        }
        catch (Exception ex)
        {
            _modalError = $"Error creating link: {ex.Message}";
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
