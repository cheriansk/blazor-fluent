using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Modules.Tenancy.Pages;

public partial class WorkflowsDashboardPage : ComponentBase
{
    [Inject] private IWorkflowService WorkflowService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IProgramService ProgramService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

private bool _isLoading = true;
    private bool _isCreateModalOpen = false;

    private List<WorkflowEntity> _allWorkflows = [];
    private List<WorkflowEntity> _filteredWorkflows = [];

    private List<ProgramEntity> _availablePrograms = [];
    private List<ProjectEntity> _availableProjects = [];
    private ProgramEntity? _filterProgram;
    private ProjectEntity? _filterProject;

    private string _selectedScope = "all";
    private string _selectedStatusFilter = "Active";
    private readonly List<string> _statusOptions = ["Active", "Completed", "All Statuses"];
    private string _searchTerm = string.Empty;

    // KPI Counters
    private int _activeWorkflowsCount;
    private int _completedWorkflowsCount;
    private int _activeStepsCount;
    private int _totalDelegatedSubtasksCount;

    // RBAC and Editing
    private bool _canCreateWorkflow;
    private WorkflowEntity? _workflowToEdit;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        try
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

                _allWorkflows = (await WorkflowService.GetWorkflowsAsync(new WorkflowFilterDto())).ToList();
                ComputeKpis();
                ApplyFilters();
                await CheckCreatePermissionsAsync();
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CheckCreatePermissionsAsync()
    {
        var level = GetDefaultCreateLevel();
        var progId = _selectedScope == "program" ? _filterProgram?.Id : null;
        var projId = _selectedScope == "project" ? _filterProject?.Id : null;
        _canCreateWorkflow = await WorkflowService.CanUserManageWorkflowAsync(level, progId, projId);
    }

    private void ComputeKpis()
    {
        _activeWorkflowsCount = _allWorkflows.Count(w => w.Status != TaskStatusEnum.Closed);
        _completedWorkflowsCount = _allWorkflows.Count(w => w.Status == TaskStatusEnum.Closed);
        _activeStepsCount = _allWorkflows.Sum(w => w.Steps.Count(s => s.Status == TaskStatusEnum.InProgress));
        _totalDelegatedSubtasksCount = _allWorkflows.Sum(w => w.Steps.Sum(s => s.SubTasks.Count));
    }

    private async Task SetScopeFilter(string scope)
    {
        _selectedScope = scope;
        ApplyFilters();
        await CheckCreatePermissionsAsync();
    }

    private async Task OnProgramFilterChanged(string? programIdStr)
    {
        _filterProgram = Guid.TryParse(programIdStr, out var progId)
            ? _availablePrograms.FirstOrDefault(p => p.Id == progId)
            : null;
        ApplyFilters();
        await CheckCreatePermissionsAsync();
    }

    private async Task OnProjectFilterChanged(string? projectIdStr)
    {
        _filterProject = Guid.TryParse(projectIdStr, out var projId)
            ? _availableProjects.FirstOrDefault(p => p.Id == projId)
            : null;
        ApplyFilters();
        await CheckCreatePermissionsAsync();
    }

    private void ApplyFilters()
    {
        var result = _allWorkflows.AsEnumerable();

        if (_selectedScope == "tenant")
        {
            result = result.Where(w => w.Level == TaskLevelEnum.Tenant);
        }
        else if (_selectedScope == "program")
        {
            result = result.Where(w => w.Level == TaskLevelEnum.Program);
            if (_filterProgram is not null)
            {
                result = result.Where(w => w.ProgramId == _filterProgram.Id);
            }
        }
        else if (_selectedScope == "project")
        {
            result = result.Where(w => w.Level == TaskLevelEnum.Project);
            if (_filterProject is not null)
            {
                result = result.Where(w => w.ProjectId == _filterProject.Id);
            }
        }

        if (_selectedStatusFilter == "Active")
        {
            result = result.Where(w => w.Status != TaskStatusEnum.Closed);
        }
        else if (_selectedStatusFilter == "Completed")
        {
            result = result.Where(w => w.Status == TaskStatusEnum.Closed);
        }

        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            var term = _searchTerm.Trim().ToLowerInvariant();
            result = result.Where(w => w.Title.ToLower().Contains(term) ||
                                       w.WorkflowOwnerEmail.ToLower().Contains(term) ||
                                       w.Steps.Any(s => s.Title.ToLower().Contains(term) || s.AssigneeEmails.ToLower().Contains(term)));
        }

        _filteredWorkflows = result.ToList();
    }

    private TaskLevelEnum GetDefaultCreateLevel() => _selectedScope switch
    {
        "tenant" => TaskLevelEnum.Tenant,
        "program" => TaskLevelEnum.Program,
        _ => TaskLevelEnum.Project
    };

    private void OpenCreateModal()
    {
        _workflowToEdit = null;
        _isCreateModalOpen = true;
    }

    private void HandleEditWorkflow(WorkflowEntity wf)
    {
        _workflowToEdit = wf;
        _isCreateModalOpen = true;
    }

    private void HandleWorkflowCreated(WorkflowEntity created)
    {
        _allWorkflows.Insert(0, created);
        ComputeKpis();
        ApplyFilters();
    }

    private void HandleWorkflowUpdated(WorkflowEntity updated)
    {
        var idx = _allWorkflows.FindIndex(w => w.Id == updated.Id);
        if (idx >= 0)
        {
            _allWorkflows[idx] = updated;
        }
        ComputeKpis();
        ApplyFilters();
    }
}