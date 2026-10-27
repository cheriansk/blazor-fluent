using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Extensions;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using TaskStatusEnum = BlazorEnterprise.Shared.DataListTypes.TaskStatusEnum;

namespace BlazorEnterprise.Modules.Tenancy.Pages;

public partial class ProjectTasksBoardPage : ComponentBase, IDisposable
{
    [Inject] private IUserTaskService TaskService { get; set; } = default!;
    [Inject] private IProjectMilestoneService MilestoneService { get; set; } = default!;
    [Inject] private IProjectHealthService HealthService { get; set; } = default!;
    [Inject] private IWorkflowService WorkflowService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

[Parameter]
    public Guid ProjectId { get; set; }

    public sealed record MilestoneFilterItem(string Value, string Label);
    public sealed record MilestoneOptionItem(string Id, string Label);

    private bool _isLoading = true;
    private bool _isSaving = false;
    private bool _isPostingComment = false;
    private bool _isAddingDependency = false;
    private bool _isCreatingMilestone = false;
    private DateTime _today = DateTime.UtcNow.Date;

    private ProjectEntity? _currentProject;
    private List<ProjectEntity> _availableProjects = [];
    private List<TaskEntity> _tasks = [];
    private List<ProjectCalendarEntity> _milestones = [];
    private List<MilestoneHealthSummaryDto> _milestonesHealth = [];
    private ProjectHealthSummaryDto _projectHealth = new();
    private Dictionary<Guid, string> _milestoneLookup = new();
    private HashSet<Guid> _activeBlockedTaskIds = [];

    private List<SwimlaneColumn<TaskEntity>> _columns = [];
    private List<MilestoneFilterItem> _milestoneFilterOptions = [];
    private List<MilestoneOptionItem> _milestoneSelectOptions = [];
    private string _selectedMilestoneFilter = "all";

    // Workflow state
    private string _activeSubTab = "tasks";
    private List<WorkflowEntity> _projectWorkflows = [];
    private bool _isCreateWorkflowModalOpen = false;
    private WorkflowEntity? _workflowToEdit;
    private bool _canManageWorkflows = false;

    // Modal state
    private bool _showCreateModal = false;
    private bool _showMilestonesModal = false;
    private string _createErrorMessage = string.Empty;
    private string _milestoneErrorMessage = string.Empty;

    private TaskEntity _newTask = new()
    {
        Level = TaskLevelEnum.Project,
        Title = string.Empty,
        Priority = TaskPriorityEnum.Medium,
        Status = TaskStatusEnum.Open,
        StartDate = DateTime.UtcNow.Date,
        DueDate = DateTime.UtcNow.Date.AddDays(7)
    };
    private DateTime? _newTaskDueDate;
    private string _newTaskMilestoneIdString = string.Empty;

    private TaskEntity? _selectedTask;
    private DateTime? _selectedTaskDueDate;
    private string _selectedTaskMilestoneIdString = string.Empty;
    private string _selectedModalTab = "details";
    private List<UserTaskCommentEntity> _selectedTaskComments = [];
    private List<TaskDependencyItemDto> _selectedTaskDependencies = [];
    private List<TaskEntity> _otherProjectTasks = [];
    private string _newCommentText = string.Empty;

    // SubTasks & Blocker Modal state
    private List<SubTaskEntity> _selectedTaskSubTasks = [];
    private string _newSubTaskTitle = string.Empty;
    private TaskPriorityEnum _newSubTaskPriority = TaskPriorityEnum.Medium;
    private DateTime? _newSubTaskDueDate = DateTime.UtcNow.Date.AddDays(3);
    private bool _isAddingSubTask = false;
    private bool _showBlockerLinkModal = false;

    // Dependency form
    private string _newDepTargetTaskId = string.Empty;
    private TaskDependencyTypeEnum _newDepType = TaskDependencyTypeEnum.Blocks;
    private DateTime? _newDepResolveByUtc;
    private string? _newDepNotes;

    // Milestone create form
    private ProjectCalendarEntity _newMilestone = new()
    {
        Title = string.Empty,
        StartDate = DateTime.UtcNow.Date,
        EndDate = DateTime.UtcNow.Date.AddDays(14),
        EntryType = CalendarEntryTypeEnum.Milestone,
        Status = CalendarEntryStatusEnum.Scheduled
    };
    private DateTime? _newMilestoneStartDate;
    private DateTime? _newMilestoneEndDate;
    private string _newMilestonePredecessorId = string.Empty;

    private IReadOnlyList<TaskEntity> FilteredTasks
    {
        get
        {
            if (_selectedMilestoneFilter == "all") return _tasks;
            if (_selectedMilestoneFilter == "adhoc") return _tasks.Where(t => t.MilestoneId == null).ToList();
            if (Guid.TryParse(_selectedMilestoneFilter, out var mid)) return _tasks.Where(t => t.MilestoneId == mid).ToList();
            return _tasks;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        TimeZoneService.OnChange += HandleTimeZoneChanged;
        InitializeSwimlanes();
        await LoadProjectsAndTasksAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_currentProject is not null && _currentProject.Id != ProjectId && ProjectId != Guid.Empty)
        {
            await LoadProjectsAndTasksAsync();
        }
    }

    private void InitializeSwimlanes()
    {
        _today = DateTime.UtcNow.Date;
        var tomorrow = _today.AddDays(1);
        var in2Days = _today.AddDays(2);

        _columns = new List<SwimlaneColumn<TaskEntity>>
        {
            new("Past Due", t => !t.IsClosed && t.Status != TaskStatusEnum.Closed && t.Status != TaskStatusEnum.Cancelled && t.DueDate.Date < _today, "#d13438", "Overdue"),
            new("Due Today", t => !t.IsClosed && t.Status != TaskStatusEnum.Closed && t.Status != TaskStatusEnum.Cancelled && t.DueDate.Date == _today, "#ff8c00", "Today's priority"),
            new("Due Tomorrow", t => !t.IsClosed && t.Status != TaskStatusEnum.Closed && t.Status != TaskStatusEnum.Cancelled && t.DueDate.Date == tomorrow, "#0078d4", "Next 24 hours"),
            new("Due in 2 Days", t => !t.IsClosed && t.Status != TaskStatusEnum.Closed && t.Status != TaskStatusEnum.Cancelled && t.DueDate.Date == in2Days, "#8764b8", "Upcoming"),
            new("Due Later", t => !t.IsClosed && t.Status != TaskStatusEnum.Closed && t.Status != TaskStatusEnum.Cancelled && t.DueDate.Date > in2Days, "#605e5c", "Future pipeline"),
            new("Closed", t => t.IsClosed || t.Status == TaskStatusEnum.Closed || t.Status == TaskStatusEnum.Cancelled, "#107c41", "Completed work")
        };
    }

    private async Task LoadProjectsAndTasksAsync()
    {
        _isLoading = true;
        try
        {
            _availableProjects = (await TenantService.GetTenantProjectsAsync(TenantContext.TenantSlug)).Where(p => p.IsActive && !p.IsDeleted).OrderBy(p => p.Name).ToList();





            if (_availableProjects.Count == 0)
            {
                _currentProject = null;
                _tasks = [];
                return;
            }

            if (ProjectId != Guid.Empty)
            {
                _currentProject = _availableProjects.FirstOrDefault(p => p.Id == ProjectId) ?? _availableProjects.First();
            }
            else if (ProjectContext.ProjectId.HasValue)
            {
                _currentProject = _availableProjects.FirstOrDefault(p => p.Id == ProjectContext.ProjectId.Value) ?? _availableProjects.First();
                ProjectId = _currentProject.Id;
            }
            else
            {
                _currentProject = _availableProjects.First();
                ProjectId = _currentProject.Id;
            }

            // Load tasks
            var result = await TaskService.GetTasksByProjectAsync(_currentProject.Id);
            _tasks = result.ToList();

            // Load milestones & health
            _milestones = (await MilestoneService.GetMilestonesByProjectAsync(_currentProject.Id)).ToList();
            _milestoneLookup = _milestones.ToDictionary(m => m.Id, m => m.Title);
            _milestonesHealth = (await HealthService.GetProjectMilestonesHealthAsync(_currentProject.Id)).ToList();
            _projectHealth = await HealthService.GetProjectHealthAsync(_currentProject.Id);

            // Load sequential workflows
            _projectWorkflows = (await WorkflowService.GetProjectWorkflowsAsync(_currentProject.Id)).ToList();
         //   _canManageWorkflows = await WorkflowService.CanUserManageWorkflowAsync(null, null, _currentProject.Id);

            // Compute active blockers
            await RefreshActiveBlockersAsync();

            // Build filter options
            BuildMilestoneOptions();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void HandleWorkflowCreated(WorkflowEntity created)
    {
        _projectWorkflows.Insert(0, created);
        StateHasChanged();
    }

    private void HandleWorkflowUpdated(WorkflowEntity updated)
    {
        var idx = _projectWorkflows.FindIndex(w => w.Id == updated.Id);
        if (idx >= 0)
        {
            _projectWorkflows[idx] = updated;
        }
        StateHasChanged();
    }

    private void HandleEditWorkflow(WorkflowEntity wf)
    {
        _workflowToEdit = wf;
        _isCreateWorkflowModalOpen = true;
    }

    private async Task RefreshActiveBlockersAsync()
    {
        if (_currentProject is null) return;

        var dependencies = (await TaskService.GetProjectWorkDependenciesAsync(_currentProject.Id)).Where(d => d.DependencyType == TaskDependencyTypeEnum.Blocks).ToList();




        var taskLookup = _tasks.ToDictionary(t => t.Id);
        _activeBlockedTaskIds = new HashSet<Guid>();

        foreach (var dep in dependencies)
        {
            if (dep.DependsOnItemId.HasValue && taskLookup.TryGetValue(dep.DependsOnItemId.Value, out var blocker))
            {
                if (!blocker.IsClosed && dep.DependentItemId.HasValue)
                {
                    _activeBlockedTaskIds.Add(dep.DependentItemId.Value);
                }
            }
            else if (!dep.IsResolved && dep.DependentItemId.HasValue)
            {
                // Unresolved cross-project, gate, or manual dependency
                _activeBlockedTaskIds.Add(dep.DependentItemId.Value);
            }
        }
    }

    private void BuildMilestoneOptions()
    {
        _milestoneFilterOptions = new List<MilestoneFilterItem>
        {
            new("all", "All Deliverables"),
            new("adhoc", "Ad-hoc Tasks (No Milestone)")
        };

        foreach (var m in _milestones)
        {
            _milestoneFilterOptions.Add(new(m.Id.ToString(), $"🚩 {m.Title} ({m.Status})"));
        }

        _milestoneSelectOptions = new List<MilestoneOptionItem>
        {
            new(string.Empty, "(None - Ad-hoc Task)")
        };

        foreach (var m in _milestones)
        {
            _milestoneSelectOptions.Add(new(m.Id.ToString(), m.Title));
        }
    }

    private void OnProjectChanged(string? newProjectIdStr)
    {
        if (Guid.TryParse(newProjectIdStr, out var newProjId) && newProjId != ProjectId)
        {
            Navigation.NavigateTo($"/projects/{newProjId}/tasks");
        }
    }

    private void OpenCreateTaskModal()
    {
        _createErrorMessage = string.Empty;
        _newTask = new TaskEntity
        {
            ProjectId = _currentProject?.Id ?? ProjectId,
            Priority = TaskPriorityEnum.Medium,
            Level = TaskLevelEnum.Project,
            Title = string.Empty,
            Status = TaskStatusEnum.Open,
            StartDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(7)
        };
        _newTaskDueDate = DateTime.UtcNow.Date.AddDays(7);
        _newTaskMilestoneIdString = _selectedMilestoneFilter != "all" && _selectedMilestoneFilter != "adhoc"
            ? _selectedMilestoneFilter
            : string.Empty;

        _showCreateModal = true;
    }

    private void OpenMilestonesModal()
    {
        _milestoneErrorMessage = string.Empty;
        _newMilestone = new ProjectCalendarEntity
        {
            ProjectId = _currentProject?.Id ?? ProjectId,
            Title = string.Empty,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(14),
            EntryType = CalendarEntryTypeEnum.Milestone,
            Status = CalendarEntryStatusEnum.Confirmed
        };
        _newMilestoneStartDate = DateTime.UtcNow.Date;
        _newMilestoneEndDate = DateTime.UtcNow.Date.AddDays(14);
        _newMilestonePredecessorId = string.Empty;
        _showMilestonesModal = true;
    }

    private void CloseModals()
    {
        _showCreateModal = false;
        _showMilestonesModal = false;
        _selectedTask = null;
        _selectedTaskComments.Clear();
        _selectedTaskDependencies.Clear();
        _otherProjectTasks.Clear();
        _newCommentText = string.Empty;
        _selectedModalTab = "details";
    }

    private async Task SaveNewTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTask.Title))
        {
            _createErrorMessage = "Task title is mandatory.";
            return;
        }

        if (!_newTaskDueDate.HasValue)
        {
            _createErrorMessage = "Due date is mandatory for all tasks.";
            return;
        }

        _isSaving = true;
        _createErrorMessage = string.Empty;
        try
        {
            _newTask.ProjectId = _currentProject!.Id;
            _newTask.DueDate = _newTaskDueDate.Value;
            _newTask.MilestoneId = Guid.TryParse(_newTaskMilestoneIdString, out var mid) ? mid : null;

            var result = await TaskService.CreateTaskAsync(_newTask);
            if (result.IsSuccess)
            {
                CloseModals();
                await ReloadDataAsync();
            }
            else
            {
                _createErrorMessage = result.Error ?? "Failed to create task.";
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task OpenTaskDetailsModal(TaskEntity task)
    {
        if (_currentProject is null) return;
        _selectedTask = await TaskService.GetTaskByIdAsync(_currentProject.Id, task.Id);
        if (_selectedTask is not null)
        {
            _selectedTaskDueDate = _selectedTask.DueDate;
            _selectedTaskMilestoneIdString = _selectedTask.MilestoneId?.ToString() ?? string.Empty;
            _selectedTaskComments = (await TaskService.GetCommentsAsync(_currentProject.Id, _selectedTask.Id)).ToList();
            _selectedTaskDependencies = (await TaskService.GetTaskDependenciesAsync(_currentProject.Id, _selectedTask.Id)).ToList();
            _selectedTaskSubTasks = (await TaskService.GetSubTasksByTaskIdAsync(_selectedTask.Id)).ToList();
            _otherProjectTasks = _tasks.Where(t => t.Id != _selectedTask.Id).ToList();

            _newDepTargetTaskId = _otherProjectTasks.FirstOrDefault()?.Id.ToString() ?? string.Empty;
            _newDepType = TaskDependencyTypeEnum.Blocks;
            _newDepResolveByUtc = _selectedTask.DueDate;
            _newDepNotes = string.Empty;
            _selectedModalTab = "details";
        }
    }

    private async Task AddSubTaskAsync()
    {
        if (_selectedTask is null || string.IsNullOrWhiteSpace(_newSubTaskTitle)) return;
        _isAddingSubTask = true;
        try
        {
            var subTask = new SubTaskEntity
            {
                ParentTaskId = _selectedTask.Id,
                Title = _newSubTaskTitle.Trim(),
                Priority = _newSubTaskPriority,
                Level = TaskLevelEnum.Project,
                StartDate = DateTime.UtcNow.Date,
                Status = TaskStatusEnum.Open,
                DueDate = _newSubTaskDueDate ?? DateTime.UtcNow.Date.AddDays(3),
                OrderIndex = _selectedTaskSubTasks.Count + 1,
                ReporterEmail = CurrentUser.Email ?? string.Empty
            };

            var result = await TaskService.CreateSubTaskAsync(subTask);
            if (result.IsSuccess)
            {
                _newSubTaskTitle = string.Empty;
                _selectedTaskSubTasks = (await TaskService.GetSubTasksByTaskIdAsync(_selectedTask.Id)).ToList();
            }
        }
        finally
        {
            _isAddingSubTask = false;
        }
    }

    private async Task ToggleSubTaskCompleteAsync(SubTaskEntity subTask, bool isCompleted)
    {
        subTask.Status = isCompleted ? TaskStatusEnum.Closed : TaskStatusEnum.Open;
        subTask.EndDate = isCompleted ? DateTime.UtcNow : null;
        await TaskService.UpdateSubTaskAsync(subTask);
        if (_selectedTask is not null)
        {
            _selectedTaskSubTasks = (await TaskService.GetSubTasksByTaskIdAsync(_selectedTask.Id)).ToList();
        }
    }

    private async Task DeleteSubTaskAsync(Guid subTaskId)
    {
        await TaskService.DeleteSubTaskAsync(subTaskId);
        if (_selectedTask is not null)
        {
            _selectedTaskSubTasks = (await TaskService.GetSubTasksByTaskIdAsync(_selectedTask.Id)).ToList();
        }
    }

    private async Task RefreshDependenciesAndLinksAsync()
    {
        if (_currentProject is null || _selectedTask is null) return;
        _selectedTaskDependencies = (await TaskService.GetTaskDependenciesAsync(_currentProject.Id, _selectedTask.Id)).ToList();
        await RefreshHealthKpisAsync();
    }

    private async Task UpdateSelectedTaskAsync()
    {
        if (_selectedTask is null) return;
        if (!_selectedTaskDueDate.HasValue) return;

        _isSaving = true;
        try
        {
            _selectedTask.DueDate = _selectedTaskDueDate.Value;
            _selectedTask.MilestoneId = Guid.TryParse(_selectedTaskMilestoneIdString, out var mid) ? mid : null;

            var result = await TaskService.UpdateTaskAsync(_selectedTask);
            if (result.IsSuccess)
            {
                CloseModals();
                await ReloadDataAsync();
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task CloseSelectedTaskAsync()
    {
        if (_selectedTask is null || _currentProject is null) return;

        var result = await TaskService.CloseTaskAsync(_currentProject.Id, _selectedTask.Id);
        if (result.IsSuccess)
        {
            CloseModals();
            await ReloadDataAsync();
        }
    }

    private async Task CancelSelectedTaskAsync()
    {
        if (_selectedTask is null || _currentProject is null) return;

        var result = await TaskService.CancelTaskAsync(_currentProject.Id, _selectedTask.Id);
        if (result.IsSuccess)
        {
            CloseModals();
            await ReloadDataAsync();
        }
    }

    private async Task ReopenSelectedTaskAsync()
    {
        if (_selectedTask is null || _currentProject is null) return;

        var result = await TaskService.ReopenTaskAsync(_currentProject.Id, _selectedTask.Id);
        if (result.IsSuccess)
        {
            CloseModals();
            await ReloadDataAsync();
        }
    }

    private async Task AddDependencyAsync()
    {
        if (_selectedTask is null || _currentProject is null) return;
        if (!Guid.TryParse(_newDepTargetTaskId, out var targetId)) return;

        _isAddingDependency = true;
        try
        {
            var result = await TaskService.AddTaskDependencyAsync(
                _currentProject.Id,
                _selectedTask.Id,
                targetId,
                _newDepType,
                _newDepResolveByUtc,
                _newDepNotes);

            if (result.IsSuccess)
            {
                _selectedTaskDependencies = (await TaskService.GetTaskDependenciesAsync(_currentProject.Id, _selectedTask.Id)).ToList();
                await RefreshActiveBlockersAsync();
                _projectHealth = await HealthService.GetProjectHealthAsync(_currentProject.Id);
            }
        }
        finally
        {
            _isAddingDependency = false;
        }
    }

    private async Task RemoveDependencyAsync(Guid depId)
    {
        if (_selectedTask is null || _currentProject is null) return;

        var result = await TaskService.RemoveTaskDependencyAsync(_currentProject.Id, depId);
        if (result.IsSuccess)
        {
            _selectedTaskDependencies = (await TaskService.GetTaskDependenciesAsync(_currentProject.Id, _selectedTask.Id)).ToList();
            await RefreshActiveBlockersAsync();
            _projectHealth = await HealthService.GetProjectHealthAsync(_currentProject.Id);
        }
    }

    private async Task PostCommentAsync()
    {
        if (_selectedTask is null || _currentProject is null || string.IsNullOrWhiteSpace(_newCommentText)) return;

        _isPostingComment = true;
        try
        {
            var result = await TaskService.AddCommentAsync(_currentProject.Id, _selectedTask.Id, _newCommentText);
            if (result.IsSuccess)
            {
                _newCommentText = string.Empty;
                _selectedTaskComments = (await TaskService.GetCommentsAsync(_currentProject.Id, _selectedTask.Id)).ToList();
            }
        }
        finally
        {
            _isPostingComment = false;
        }
    }

    private async Task CreateMilestoneAsync()
    {
        if (string.IsNullOrWhiteSpace(_newMilestone.Title))
        {
            _milestoneErrorMessage = "Milestone name is required.";
            return;
        }

        if (!_newMilestoneStartDate.HasValue || !_newMilestoneEndDate.HasValue)
        {
            _milestoneErrorMessage = "Start and End dates are required.";
            return;
        }

        if (_newMilestoneEndDate.Value < _newMilestoneStartDate.Value)
        {
            _milestoneErrorMessage = "End date cannot precede Start date.";
            return;
        }

        _isCreatingMilestone = true;
        _milestoneErrorMessage = string.Empty;
        try
        {
            _newMilestone.ProjectId = _currentProject!.Id;
            _newMilestone.StartDate = _newMilestoneStartDate.Value;
            _newMilestone.EndDate = _newMilestoneEndDate.Value;
            _newMilestone.Status = CalendarEntryStatusEnum.Confirmed;
            _newMilestone.EntryType = CalendarEntryTypeEnum.Milestone;

            var res = await MilestoneService.CreateMilestoneAsync(_newMilestone);
            if (res.IsSuccess)
            {
                if (Guid.TryParse(_newMilestonePredecessorId, out var predId) && predId != Guid.Empty)
                {
                    await MilestoneService.AddMilestoneDependencyAsync(_newMilestone.Id, predId);
                }

                _showMilestonesModal = false;
                await ReloadDataAsync();
            }
            else
            {
                _milestoneErrorMessage = res.Error ?? "Failed to create milestone.";
            }
        }
        finally
        {
            _isCreatingMilestone = false;
        }
    }

    private async Task SignOffMilestoneAsync(Guid mid)
    {
        var res = await MilestoneService.CompleteMilestoneAsync(mid);
        if (res.IsSuccess)
        {
            await ReloadDataAsync();
        }
    }

    private async Task DeleteMilestoneAsync(Guid mid)
    {
        var res = await MilestoneService.DeleteMilestoneAsync(mid);
        if (res.IsSuccess)
        {
            await ReloadDataAsync();
        }
    }

    private async Task RefreshHealthKpisAsync()
    {
        if (_currentProject is null) return;
        _milestonesHealth = (await HealthService.GetProjectMilestonesHealthAsync(_currentProject.Id)).ToList();
        _projectHealth = await HealthService.GetProjectHealthAsync(_currentProject.Id);
    }

    private async Task ReloadDataAsync()
    {
        if (_currentProject is null) return;
        var freshTasks = await TaskService.GetTasksByProjectAsync(_currentProject.Id);
        _tasks = freshTasks.ToList();

        _milestones = (await MilestoneService.GetMilestonesByProjectAsync(_currentProject.Id)).ToList();
        _milestoneLookup = _milestones.ToDictionary(m => m.Id, m => m.Title);
        await RefreshHealthKpisAsync();

        await RefreshActiveBlockersAsync();
        BuildMilestoneOptions();
    }

    private static string GetPriorityColor(TaskPriorityEnum priority) => priority switch
    {
        TaskPriorityEnum.Urgent => "#d13438", // Red
        TaskPriorityEnum.High => "#ff8c00",   // Orange
        TaskPriorityEnum.Medium => "#0078d4", // Blue
        _ => "#605e5c"                    // Gray
    };

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
    }
}