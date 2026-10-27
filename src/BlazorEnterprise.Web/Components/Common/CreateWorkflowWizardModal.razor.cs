using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.UI.Wizards;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Components.Common;

public partial class CreateWorkflowWizardModal : ComponentBase
{
    [Inject] private IWorkflowService WorkflowService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IProgramService ProgramService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public EventCallback<bool> IsOpenChanged { get; set; }

    [Parameter]
    public Guid? ProjectId { get; set; }

    [Parameter]
    public TaskLevelEnum DefaultLevel { get; set; } = TaskLevelEnum.Project;

    [Parameter]
    public WorkflowEntity? WorkflowToEdit { get; set; }

    [Parameter]
    public EventCallback<WorkflowEntity> OnWorkflowCreated { get; set; }

    [Parameter]
    public EventCallback<WorkflowEntity> OnWorkflowUpdated { get; set; }

    private bool IsEditMode => WorkflowToEdit != null;

    private CreateWorkflowReqDto _initialState = new();
    private CskWizardModal<CreateWorkflowReqDto>? _wizardModal;
    private Dictionary<string, string> _stepErrors = new(StringComparer.OrdinalIgnoreCase);
    private List<ProgramEntity> _availablePrograms = [];
    private List<ProjectEntity> _availableProjects = [];
    private ProgramEntity? _selectedProgram;
    private ProjectEntity? _selectedProject;

    // Task Search & Attach State
    private bool _showTaskSearch;
    private string _taskSearchTerm = string.Empty;
    private bool _isSearchingTasks;
    private List<TaskEntity> _availableExistingTasks = [];

    // Step Cancellation Modal State
    private bool _showCancelStepModal;
    private CreateWorkflowStepDto? _stepToCancel;
    private string _cancelStepReason = string.Empty;
    private string? _cancelStepError;

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen)
        {
            await LoadScopingEntitiesAsync();
            if (WorkflowToEdit != null)
            {
                PopulateFromExistingWorkflow(WorkflowToEdit);
            }
            else if (_initialState.ProjectId != ProjectId || _initialState.Steps.Count == 0)
            {
                ResetState();
            }
        }
    }

    private async Task LoadScopingEntitiesAsync()
    {
        if (TenantContext.HasActiveTenant)
        {
            var tenantId = TenantContext.TenantId;
            _availablePrograms = (await ProgramService.GetProgramsAsync(TenantContext.TenantSlug))
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToList();

            _availableProjects = (await TenantService.GetTenantProjectsAsync(TenantContext.TenantSlug))
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToList();

            if (ProjectId.HasValue && ProjectId.Value != Guid.Empty)
            {
                _selectedProject = _availableProjects.FirstOrDefault(p => p.Id == ProjectId.Value);
            }
        }
    }

    private void OnProgramSelectedChanged(string? val)
    {
        if (Guid.TryParse(val, out var progId))
        {
            _initialState.ProgramId = progId;
            _selectedProgram = _availablePrograms.FirstOrDefault(p => p.Id == progId);
        }
        else
        {
            _initialState.ProgramId = null;
            _selectedProgram = null;
        }
        _wizardModal?.Context?.MarkDirty();
    }

    private void OnProjectSelectedChanged(string? val)
    {
        if (Guid.TryParse(val, out var projId))
        {
            _initialState.ProjectId = projId;
            _selectedProject = _availableProjects.FirstOrDefault(p => p.Id == projId);
        }
        else
        {
            _initialState.ProjectId = null;
            _selectedProject = null;
        }
        _wizardModal?.Context?.MarkDirty();
    }

    private void PopulateFromExistingWorkflow(WorkflowEntity wf)
    {
        _initialState = new CreateWorkflowReqDto
        {
            Level = wf.Level,
            ProgramId = wf.ProgramId,
            ProjectId = wf.ProjectId,
            Title = wf.Title,
            Description = wf.Description,
            WorkflowOwnerEmail = wf.WorkflowOwnerEmail,
            Priority = wf.Priority,
            DueDate = wf.DueDate,
            WatcherEmails = wf.WatcherEmails,
            Steps = wf.Steps
                .OrderBy(s => s.WorkflowStepOrder ?? 1)
                .Select(s => new CreateWorkflowStepDto
                {
                    ExistingTaskId = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    StepOwnerEmail = s.AssigneeEmails,
                    DueDate = s.DueDate,
                    SubTasks = s.SubTasks
                        .OrderBy(st => st.OrderIndex)
                        .Select(st => new CreateWorkflowSubTaskDto
                        {
                            Title = st.Title,
                            AssigneeEmail = st.AssigneeEmails,
                            DueDate = st.DueDate
                        }).ToList()
                }).ToList()
        };

        if (wf.ProjectId.HasValue)
        {
            _selectedProject = _availableProjects.FirstOrDefault(p => p.Id == wf.ProjectId.Value);
        }
        if (wf.ProgramId.HasValue)
        {
            _selectedProgram = _availablePrograms.FirstOrDefault(p => p.Id == wf.ProgramId.Value);
        }
    }

    private void ResetState()
    {
        _initialState = new CreateWorkflowReqDto
        {
            Level = DefaultLevel,
            ProjectId = ProjectId,
            WorkflowOwnerEmail = CurrentUser.Email ?? "orchestrator@company.local",
            Priority = TaskPriorityEnum.Medium,
            DueDate = DateTime.UtcNow.AddDays(14),
            Steps = new List<CreateWorkflowStepDto>
            {
                new()
                {
                    Title = "Initial Analysis & Feasibility",
                    StepOwnerEmail = CurrentUser.Email ?? "engineer@company.local",
                    DueDate = DateTime.UtcNow.AddDays(3),
                    Description = "Assess technical scope, requirements, and architecture.",
                    SubTasks = new List<CreateWorkflowSubTaskDto>
                    {
                        new() { Title = "Architecture Review", AssigneeEmail = CurrentUser.Email ?? "architect@company.local" },
                        new() { Title = "Security Threat Modeling", AssigneeEmail = "security@company.local" }
                    }
                },
                new()
                {
                    Title = "Stakeholder Approval & Sign-Off",
                    StepOwnerEmail = "lead@company.local",
                    DueDate = DateTime.UtcNow.AddDays(7),
                    Description = "Verify sign-off checklist and approve final rollout."
                }
            }
        };

        if (ProjectId.HasValue && _availableProjects.Count > 0)
        {
            _selectedProject = _availableProjects.FirstOrDefault(p => p.Id == ProjectId.Value) ?? _availableProjects.FirstOrDefault();
            _initialState.ProjectId = _selectedProject?.Id;
        }
        if (_availablePrograms.Count > 0)
        {
            _selectedProgram = _availablePrograms.FirstOrDefault();
            if (_initialState.Level == TaskLevelEnum.Program)
            {
                _initialState.ProgramId = _selectedProgram?.Id;
            }
        }
    }

    private void ToggleTaskSearch()
    {
        _showTaskSearch = !_showTaskSearch;
        if (_showTaskSearch && _availableExistingTasks.Count == 0)
        {
            _ = SearchExistingTasksAsync();
        }
    }

    private async Task SearchExistingTasksAsync()
    {
        _isSearchingTasks = true;
        try
        {
            var filter = new TaskSearchFilterDto(
                Level: _initialState.Level,
                ProgramId: _initialState.ProgramId,
                ProjectId: _initialState.ProjectId,
                SearchTerm: _taskSearchTerm,
                ExcludeTasksWithWorkflows: true
            );
            var results = await WorkflowService.GetAvailableTasksForWorkflowAsync(filter);
            var currentTaskIds = _initialState.Steps
                .Where(s => s.ExistingTaskId.HasValue)
                .Select(s => s.ExistingTaskId!.Value)
                .ToHashSet();

            _availableExistingTasks = results.Where(t => !currentTaskIds.Contains(t.Id)).ToList();
        }
        finally
        {
            _isSearchingTasks = false;
        }
    }

    private void AttachExistingTask(TaskEntity task)
    {
        _initialState.Steps.Add(new CreateWorkflowStepDto
        {
            ExistingTaskId = task.Id,
            Title = task.Title,
            Description = task.Description,
            StepOwnerEmail = !string.IsNullOrWhiteSpace(task.AssigneeEmails) ? task.AssigneeEmails : (CurrentUser.Email ?? string.Empty),
            DueDate = task.DueDate.Year > 2000 ? task.DueDate : DateTime.UtcNow.AddDays(7),
            SubTasks = task.SubTasks
                .OrderBy(s => s.OrderIndex)
                .Select(s => new CreateWorkflowSubTaskDto
                {
                    Title = s.Title,
                    AssigneeEmail = s.AssigneeEmails,
                    DueDate = s.DueDate
                }).ToList()
        });

        _availableExistingTasks.RemoveAll(t => t.Id == task.Id);
        _showTaskSearch = false;
        _wizardModal?.Context?.MarkDirty();
    }

    private Task<bool> ValidateStep1Async()
    {
        _stepErrors.Clear();
        if (string.IsNullOrWhiteSpace(_initialState.Title))
        {
            _stepErrors["Title"] = "Workflow title is required.";
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(_initialState.WorkflowOwnerEmail) || !_initialState.WorkflowOwnerEmail.Contains('@'))
        {
            _stepErrors["WorkflowOwnerEmail"] = "A valid constant Workflow Owner email is required.";
            return Task.FromResult(false);
        }

        if (_initialState.Level == TaskLevelEnum.Program && (!_initialState.ProgramId.HasValue || _initialState.ProgramId == Guid.Empty))
        {
            _stepErrors["ProgramId"] = "Please select a target Program.";
            return Task.FromResult(false);
        }

        if (_initialState.Level == TaskLevelEnum.Project && (!_initialState.ProjectId.HasValue || _initialState.ProjectId == Guid.Empty))
        {
            _stepErrors["ProjectId"] = "Please select a target Project.";
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private Task<bool> ValidateStep2Async()
    {
        _stepErrors.Clear();
        if (_initialState.Steps == null || _initialState.Steps.Count == 0)
        {
            _stepErrors["Steps"] = "At least one sequential step (task) is required.";
            return Task.FromResult(false);
        }

        for (int i = 0; i < _initialState.Steps.Count; i++)
        {
            var s = _initialState.Steps[i];
            if (string.IsNullOrWhiteSpace(s.Title))
            {
                _stepErrors[$"Step_{i}_Title"] = $"Step #{i + 1} requires a title.";
                return Task.FromResult(false);
            }
            if (string.IsNullOrWhiteSpace(s.StepOwnerEmail) || !s.StepOwnerEmail.Contains('@'))
            {
                _stepErrors[$"Step_{i}_Owner"] = $"Step #{i + 1} requires a valid Step Owner email.";
                return Task.FromResult(false);
            }
        }
        return Task.FromResult(true);
    }

    private void AddNewStep()
    {
        _initialState.Steps.Add(new CreateWorkflowStepDto
        {
            Title = $"Step #{_initialState.Steps.Count + 1}",
            StepOwnerEmail = string.Empty,
            DueDate = DateTime.UtcNow.AddDays(7),
            Description = string.Empty
        });
        _wizardModal?.Context?.MarkDirty();
    }

    private void MoveStepUp(int index)
    {
        if (index > 0 && index < _initialState.Steps.Count)
        {
            var item = _initialState.Steps[index];
            _initialState.Steps.RemoveAt(index);
            _initialState.Steps.Insert(index - 1, item);
            _wizardModal?.Context?.MarkDirty();
        }
    }

    private void MoveStepDown(int index)
    {
        if (index >= 0 && index < _initialState.Steps.Count - 1)
        {
            var item = _initialState.Steps[index];
            _initialState.Steps.RemoveAt(index);
            _initialState.Steps.Insert(index + 1, item);
            _wizardModal?.Context?.MarkDirty();
        }
    }

    private void RemoveStep(int index)
    {
        if (_initialState.Steps.Count > 1 && index >= 0 && index < _initialState.Steps.Count)
        {
            _initialState.Steps.RemoveAt(index);
            _wizardModal?.Context?.MarkDirty();
        }
    }

    private void AddSubTaskToStep(CreateWorkflowStepDto step)
    {
        step.SubTasks.Add(new CreateWorkflowSubTaskDto
        {
            Title = string.Empty,
            AssigneeEmail = string.Empty,
            DueDate = step.DueDate
        });
        _wizardModal?.Context?.MarkDirty();
    }

    private void RemoveSubTaskFromStep(CreateWorkflowStepDto step, int subIndex)
    {
        if (subIndex >= 0 && subIndex < step.SubTasks.Count)
        {
            step.SubTasks.RemoveAt(subIndex);
            _wizardModal?.Context?.MarkDirty();
        }
    }

    private void PromptCancelExistingStep(CreateWorkflowStepDto step)
    {
        _stepToCancel = step;
        _cancelStepReason = string.Empty;
        _cancelStepError = null;
        _showCancelStepModal = true;
    }

    private async Task ConfirmCancelExistingStepAsync()
    {
        if (_stepToCancel == null || WorkflowToEdit == null || !_stepToCancel.ExistingTaskId.HasValue) return;

        if (string.IsNullOrWhiteSpace(_cancelStepReason))
        {
            _cancelStepError = "Cancellation reason is mandatory.";
            return;
        }

        try
        {
            var updated = await WorkflowService.CancelStepAsync(new CancelWorkflowStepReqDto(
                WorkflowToEdit.Id,
                _stepToCancel.ExistingTaskId.Value,
                _cancelStepReason.Trim()
            ));

            _showCancelStepModal = false;
            _stepToCancel = null;
            await OnWorkflowUpdated.InvokeAsync(updated);
            PopulateFromExistingWorkflow(updated);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _cancelStepError = ex.Message;
        }
    }

    private async Task HandleFinishAsync(CreateWorkflowReqDto state)
    {
        if (IsEditMode && WorkflowToEdit != null)
        {
            var editReq = new EditWorkflowReqDto
            {
                WorkflowId = WorkflowToEdit.Id,
                Title = state.Title,
                Description = state.Description,
                WorkflowOwnerEmail = state.WorkflowOwnerEmail,
                Priority = state.Priority,
                DueDate = state.DueDate,
                WatcherEmails = state.WatcherEmails
            };
            var updated = await WorkflowService.EditWorkflowAsync(editReq);

            var existingStepIds = WorkflowToEdit.Steps.Select(s => s.Id).ToHashSet();
            var newSteps = state.Steps.Where(s => !s.ExistingTaskId.HasValue || !existingStepIds.Contains(s.ExistingTaskId.Value)).ToList();

            foreach (var newStep in newSteps)
            {
                await WorkflowService.AppendStepAsync(new AppendStepToWorkflowReqDto(
                    WorkflowId: WorkflowToEdit.Id,
                    ExistingTaskId: newStep.ExistingTaskId,
                    NewTaskTitle: newStep.Title,
                    NewTaskOwnerEmail: newStep.StepOwnerEmail,
                    DeliverableNotes: newStep.Description
                ));
            }

            var reloaded = await WorkflowService.GetWorkflowByIdAsync(WorkflowToEdit.Id) ?? updated;
            await OnWorkflowUpdated.InvokeAsync(reloaded);
        }
        else
        {
            var created = await WorkflowService.CreateWorkflowAsync(state);
            await OnWorkflowCreated.InvokeAsync(created);
        }

        await IsOpenChanged.InvokeAsync(false);
        ResetState();
    }

    private async Task HandleCancel()
    {
        await IsOpenChanged.InvokeAsync(false);
        ResetState();
    }
}
