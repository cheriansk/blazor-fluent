using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Components.Common;

public partial class WorkflowStepperComp : ComponentBase
{
    [Inject] private IWorkflowService WorkflowService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private INotifyService NotifyService { get; set; } = default!;
    [Inject] private IHierarchyAuthorizationService HierarchyAuth { get; set; } = default!;

    [Parameter, EditorRequired]
    public WorkflowEntity Workflow { get; set; } = default!;

    [Parameter]
    public EventCallback<WorkflowEntity> OnWorkflowUpdated { get; set; }

    [Parameter]
    public EventCallback<WorkflowEntity> OnEditWorkflow { get; set; }

    private bool _isCompleteModalOpen;
    private bool _isReassignModalOpen;
    private bool _isAddSubtaskModalOpen;
    private bool _isCancelModalOpen;

    private TaskEntity? _targetStep;
    private string _completionNotes = string.Empty;
    private string _reassignEmail = string.Empty;
    private string _reassignReason = string.Empty;
    private string _cancelReason = string.Empty;

    private string _newSubtaskTitle = string.Empty;
    private string _newSubtaskAssignee = string.Empty;
    private DateTime? _newSubtaskDueDate;

    private string? _errorMessage;
    private bool _isSubmitting;
    private bool _isTenantAdmin;
    private bool _canManage;

    protected override async Task OnParametersSetAsync()
    {
        if (Workflow != null && !string.IsNullOrWhiteSpace(CurrentUser.UserId))
        {
            _isTenantAdmin = await HierarchyAuth.IsTenantAdminAsync(Workflow.TenantId, CurrentUser.UserId);
            _canManage = await WorkflowService.CanUserManageWorkflowAsync(Workflow.Level, Workflow.ProgramId, Workflow.ProjectId);
        }
        else
        {
            _isTenantAdmin = false;
            _canManage = false;
        }
    }

    private string GetPriorityColor(TaskPriorityEnum priority) => priority switch
    {
        TaskPriorityEnum.Urgent => "var(--error, #d13438)",
        TaskPriorityEnum.High => "#d83b01",
        TaskPriorityEnum.Medium => "#ffaa44",
        _ => "var(--neutral-stroke-subtle, #8a8886)"
    };

    private bool CanToggleSubtask(TaskEntity step, SubTaskEntity sub)
    {
        if (_isTenantAdmin || CurrentUser.IsRootAdmin) return true;
        if (string.Equals(CurrentUser.Email, Workflow.WorkflowOwnerEmail, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(CurrentUser.Email, step.AssigneeEmails, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(CurrentUser.Email, sub.AssigneeEmails, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private async Task ToggleSubtaskAsync(SubTaskEntity sub, bool isClosed)
    {
        try
        {
            var result = await WorkflowService.ToggleSubTaskStatusAsync(new ToggleSubTaskStatusReqDto(sub.Id, isClosed));
            if (!result.IsSuccess) { _errorMessage = result.Error; return; }
            var updated = result.Value!;
            sub.Status = updated.Status;
            sub.EndDate = updated.EndDate;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private void PromptAddSubtask(TaskEntity step)
    {
        _targetStep = step;
        _newSubtaskTitle = string.Empty;
        _newSubtaskAssignee = string.Empty;
        _newSubtaskDueDate = DateTime.UtcNow.AddDays(2);
        _errorMessage = null;
        _isAddSubtaskModalOpen = true;
    }

    private void CancelAddSubtask()
    {
        _isAddSubtaskModalOpen = false;
        _targetStep = null;
        _errorMessage = null;
    }

    private async Task ConfirmAddSubtaskAsync()
    {
        if (_targetStep is null) return;
        if (string.IsNullOrWhiteSpace(_newSubtaskTitle))
        {
            _errorMessage = "Subtask title is required.";
            return;
        }

        _isSubmitting = true;
        _errorMessage = null;

        try
        {
            var req = new AddSubTaskToStepReqDto(
                Workflow.Id,
                _targetStep.Id,
                _newSubtaskTitle.Trim(),
                _newSubtaskAssignee.Trim(),
                _newSubtaskDueDate
            );

            var result = await WorkflowService.AddSubTaskToStepAsync(req);
            if (!result.IsSuccess) { _errorMessage = result.Error; return; }
            var created = result.Value!;
            _targetStep.SubTasks.Add(created);
            _isAddSubtaskModalOpen = false;
            _targetStep = null;

            await OnWorkflowUpdated.InvokeAsync(Workflow);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void PromptCompleteStep(TaskEntity step)
    {
        _targetStep = step;
        _completionNotes = string.Empty;
        _errorMessage = null;
        _isCompleteModalOpen = true;
    }

    private void CancelCompleteStep()
    {
        _isCompleteModalOpen = false;
        _targetStep = null;
        _errorMessage = null;
    }

    private async Task ConfirmCompleteStepAsync()
    {
        if (_targetStep is null) return;

        _isSubmitting = true;
        _errorMessage = null;

        try
        {
            var req = new CompleteWorkflowStepReqDto(
                Workflow.Id,
                _targetStep.Id,
                _completionNotes
            );

            var result = await WorkflowService.CompleteStepAsync(req);
            if (!result.IsSuccess) { _errorMessage = result.Error; return; }
            _isCompleteModalOpen = false;
            _targetStep = null;

            await OnWorkflowUpdated.InvokeAsync(result.Value!);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void PromptReassignStep(TaskEntity step)
    {
        _targetStep = step;
        _reassignEmail = string.Empty;
        _reassignReason = string.Empty;
        _errorMessage = null;
        _isReassignModalOpen = true;
    }

    private void CancelReassignStep()
    {
        _isReassignModalOpen = false;
        _targetStep = null;
        _errorMessage = null;
    }

    private async Task ConfirmReassignStepAsync()
    {
        if (_targetStep is null) return;

        if (string.IsNullOrWhiteSpace(_reassignEmail) || !_reassignEmail.Contains('@'))
        {
            _errorMessage = "A valid new owner email address is required.";
            return;
        }

        _isSubmitting = true;
        _errorMessage = null;

        try
        {
            var req = new ReassignWorkflowStepReqDto(
                Workflow.Id,
                _targetStep.Id,
                _reassignEmail.Trim(),
                _reassignReason
            );

            var result = await WorkflowService.ReassignStepAsync(req);
            if (!result.IsSuccess) { _errorMessage = result.Error; return; }
            _isReassignModalOpen = false;
            _targetStep = null;

            await OnWorkflowUpdated.InvokeAsync(result.Value!);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void PromptCancelStep(TaskEntity step)
    {
        _targetStep = step;
        _cancelReason = string.Empty;
        _errorMessage = null;
        _isCancelModalOpen = true;
    }

    private void CancelCancelStep()
    {
        _isCancelModalOpen = false;
        _targetStep = null;
        _errorMessage = null;
    }

    private async Task ConfirmCancelStepAsync()
    {
        if (_targetStep is null) return;
        if (string.IsNullOrWhiteSpace(_cancelReason))
        {
            _errorMessage = "A mandatory cancellation reason is required.";
            return;
        }

        _isSubmitting = true;
        _errorMessage = null;

        try
        {
            var req = new CancelWorkflowStepReqDto(
                Workflow.Id,
                _targetStep.Id,
                _cancelReason.Trim()
            );

            var result = await WorkflowService.CancelStepAsync(req);
            if (!result.IsSuccess) { _errorMessage = result.Error; return; }
            _isCancelModalOpen = false;
            _targetStep = null;

            await OnWorkflowUpdated.InvokeAsync(result.Value!);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task PromptResumeWorkflowAsync()
    {
        _isSubmitting = true;
        _errorMessage = null;

        try
        {
            var result = await WorkflowService.ResumePausedWorkflowAsync(new ResumePausedWorkflowReqDto(Workflow.Id));
            if (!result.IsSuccess) { _errorMessage = result.Error; return; }
            await OnWorkflowUpdated.InvokeAsync(result.Value!);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
