using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos.Tasks;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorEnterprise.Web.Components.Dialogs;

public partial class TaskCreationDialog : ComponentBase
{
    [Inject] private ITaskCreationService TaskCreationService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IDateTimeProvider DateTimeProvider { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public CreationTaskTypeEnum DefaultTaskType { get; set; } = CreationTaskTypeEnum.SprintTask;
    [Parameter] public Guid? ProjectId { get; set; }
    [Parameter] public Guid? ProgramId { get; set; }
    [Parameter] public Guid? SprintId { get; set; }
    [Parameter] public EventCallback<TaskCreationRequestDto> OnTaskCreated { get; set; }

    private TaskCreationRequestDto _request = new();
    private TaskCreationRelatedDataDto _relatedData = new();
    private List<string> _errors = [];
    private bool _isSubmitting;
    private bool _showSuccessMessage;
    private string _createdTaskTitle = string.Empty;

    private string? _estimatedHoursText;
    private string? _storyPointsText;

    private int ExposureScore => TaskCreationService.CalculateRiskExposure(_request.Probability, _request.Impact);

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen)
        {
            await InitializeFormAsync();
        }
    }

    private async Task InitializeFormAsync()
    {
        _errors.Clear();
        _showSuccessMessage = false;

        var now = DateTimeProvider.UtcNow;
        _request = new TaskCreationRequestDto
        {
            TaskType = DefaultTaskType,
            TenantId = TenantContext.HasActiveTenant ? TenantContext.TenantId : Guid.Empty,
            ProjectId = ProjectId,
            ProgramId = ProgramId,
            SprintId = SprintId,
            StartDate = now.Date,
            DueDate = now.Date.AddDays(7),
            Status = TaskStatusEnum.Open,
            Priority = TaskPriorityEnum.Medium,
            Probability = RiskProbabilityEnum.Medium,
            Impact = RiskImpactEnum.Medium,
            ValidationStatus = AssumptionValidationStatusEnum.Unconfirmed,
            Severity = IssueSeverityEnum.Medium,
            AssigneeEmails = CurrentUser.IsAuthenticated ? CurrentUser.Email : null
        };

        _estimatedHoursText = null;
        _storyPointsText = null;

        if (ProjectId.HasValue && ProjectId.Value != Guid.Empty)
        {
            _relatedData = await TaskCreationService.GetRelatedDataAsync(ProjectId.Value, _request.TaskType);
        }
    }

    private void OnEstimatedHoursChanged(string? value)
    {
        _estimatedHoursText = value;
        if (decimal.TryParse(value, out var hours))
        {
            _request.EstimatedHours = hours;
        }
        else
        {
            _request.EstimatedHours = null;
        }
    }

    private void OnStoryPointsChanged(string? value)
    {
        _storyPointsText = value;
        if (double.TryParse(value, out var pts))
        {
            _request.StoryPoints = pts;
        }
        else
        {
            _request.StoryPoints = null;
        }
    }

    private async Task SubmitAsync()
    {
        _errors.Clear();
        _isSubmitting = true;

        try
        {
            var validation = await TaskCreationService.ValidateTaskDataAsync(_request);
            if (!validation.Succeeded)
            {
                _errors = validation.Errors ?? ["Validation failed."];
                return;
            }

            var result = await TaskCreationService.CreateTaskAsync(_request);
            if (result.Succeeded && result.Data != null)
            {
                _createdTaskTitle = result.Data.Title;
                _showSuccessMessage = true;
                await OnTaskCreated.InvokeAsync(_request);
            }
            else
            {
                _errors = result.Errors ?? [result.Message ?? "Failed to create work item."];
            }
        }
        catch (Exception ex)
        {
            _errors = [ex.Message];
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task ResetForAnotherAsync()
    {
        await InitializeFormAsync();
    }

    private async Task CloseAsync()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    private async Task HandleBackdropClick()
    {
        await CloseAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await CloseAsync();
        }
        else if (e.Key == "Enter" && e.CtrlKey)
        {
            await SubmitAsync();
        }
    }

    private static string GetTaskTypeIcon(CreationTaskTypeEnum type) => type switch
    {
        CreationTaskTypeEnum.SprintTask => CskIcons.TaskListSquare,
        CreationTaskTypeEnum.ActivityTask => CskIcons.Document,
        CreationTaskTypeEnum.Risk => CskIcons.Shield,
        CreationTaskTypeEnum.Assumption => CskIcons.LightbulbPulse,
        CreationTaskTypeEnum.Issue => CskIcons.Warning,
        _ => CskIcons.Document
    };

    private static string GetTaskTypeBadgeClass(CreationTaskTypeEnum type) => type switch
    {
        CreationTaskTypeEnum.SprintTask => "badge-sprint",
        CreationTaskTypeEnum.ActivityTask => "badge-activity",
        CreationTaskTypeEnum.Risk => "badge-risk",
        CreationTaskTypeEnum.Assumption => "badge-assumption",
        CreationTaskTypeEnum.Issue => "badge-issue",
        _ => "badge-default"
    };

    private static string GetExposureBadgeClass(int score) => score switch
    {
        >= 8 => "exposure-high",
        >= 4 => "exposure-med",
        _ => "exposure-low"
    };

    private static string GetExposureLabel(int score) => score switch
    {
        >= 8 => "High / Critical",
        >= 4 => "Moderate",
        _ => "Low"
    };
}
