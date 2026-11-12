using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Core.Dtos.Tasks;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

public class TaskCreationService : ITaskCreationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<TaskCreationService> _logger;

    public TaskCreationService(
        IDbContextFactory<AppDbContext> dbFactory,
        IUnitOfWork unitOfWork,
        IProjectAuthorizationService projectAuth,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ILogger<TaskCreationService> logger)
    {
        _dbFactory = dbFactory;
        _unitOfWork = unitOfWork;
        _projectAuth = projectAuth;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<BaseTaskEntity>> CreateTaskAsync(TaskCreationRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await ValidateTaskDataAsync(request, ct);
        if (!validationResult.Succeeded)
        {
            return Result<BaseTaskEntity>.Failure(validationResult.Errors);
        }

        var tenantId = request.TenantId != Guid.Empty
            ? request.TenantId
            : (_tenantContext.HasActiveTenant ? _tenantContext.TenantId : Guid.Empty);

        if (tenantId == Guid.Empty)
        {
            return Result<BaseTaskEntity>.Failure("Active Tenant is required to create work items.");
        }

        if (request.ProjectId.HasValue && request.ProjectId.Value != Guid.Empty)
        {
            await _projectAuth.EnsureCanEditAsync(request.ProjectId.Value, ProjectRoleEnum.Contribute, "CreateWorkItem", ct);
        }

        BaseTaskEntity createdEntity;

        var txResult = await _unitOfWork.ExecuteInTransactionAsync<BaseTaskEntity>(async (scope, token) =>
        {
            var db = await _dbFactory.CreateDbContextAsync(token);

            // Strict Hierarchy Scoping Invariants (BaseTaskEntityValidator):
            // 1. Project level: ProjectId mandatory, ProgramId strictly null (inherited via Project.ProgramId).
            // 2. Program level: ProgramId mandatory, ProjectId strictly null.
            // 3. Tenant level: strictly no ProgramId or ProjectId.
            var targetLevel = request.ProjectId.HasValue
                ? TaskLevelEnum.Project
                : (request.ProgramId.HasValue && request.ProgramId.Value != Guid.Empty ? TaskLevelEnum.Program : TaskLevelEnum.Tenant);

            var scopedProjectId = targetLevel == TaskLevelEnum.Project ? request.ProjectId : null;
            var scopedProgramId = targetLevel == TaskLevelEnum.Program ? request.ProgramId : null;

            var now = DateTime.UtcNow;
            var startDate = request.StartDate ?? now;
            var dueDate = request.DueDate ?? startDate.AddDays(7);

            switch (request.TaskType)
            {
                case CreationTaskTypeEnum.SprintTask:
                    if (!request.SprintId.HasValue || request.SprintId.Value == Guid.Empty)
                    {
                        return Result<BaseTaskEntity>.Failure("Sprint is required for Sprint Tasks.");
                    }

                    var sprintTask = new SprintTaskEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        ProgramId = scopedProgramId,
                        ProjectId = scopedProjectId,
                        Level = TaskLevelEnum.Project,
                        Title = request.Title.Trim(),
                        Description = request.Description?.Trim(),
                        Priority = request.Priority,
                        Status = request.Status,
                        StartDate = startDate,
                        DueDate = dueDate,
                        EndDate = dueDate,
                        AssigneeEmails = request.AssigneeEmails?.Trim(),
                        EstimatedHours = request.EstimatedHours,
                        Labels = request.Labels?.Trim(),
                        MilestoneId = request.MilestoneId,
                        SprintId = request.SprintId.Value,
                        ReporterEmail = _currentUser.Email
                    };

                    scope.StageInsert(sprintTask);
                    createdEntity = sprintTask;
                    break;

                case CreationTaskTypeEnum.ActivityTask:
                    var activityTask = new ActivityTaskEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        ProgramId = scopedProgramId,
                        ProjectId = scopedProjectId,
                        Level = targetLevel,
                        Title = request.Title.Trim(),
                        Description = request.Description?.Trim(),
                        Priority = request.Priority,
                        Status = request.Status,
                        StartDate = startDate,
                        DueDate = dueDate,
                        EndDate = dueDate,
                        AssigneeEmails = request.AssigneeEmails?.Trim(),
                        EstimatedHours = request.EstimatedHours,
                        Labels = request.Labels?.Trim(),
                        MilestoneId = request.MilestoneId,
                        ReporterEmail = _currentUser.Email
                    };

                    if (request.ActionItems != null && request.ActionItems.Count > 0)
                    {
                        int order = 0;
                        foreach (var item in request.ActionItems)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Title))
                            {
                                scope.StageInsert(new ActionItemEntity
                                {
                                    Id = Guid.CreateVersion7(),
                                    TenantId = tenantId,
                                    ProgramId = scopedProgramId,
                                    ProjectId = scopedProjectId,
                                    Level = targetLevel,
                                    ParentTaskId = activityTask.Id,
                                    ParentSubTaskId = null,
                                    Title = item.Title.Trim(),
                                    Description = item.Description?.Trim(),
                                    Status = item.Status == 0 ? ActionItemStatusEnum.Open : item.Status,
                                    Complexity = item.Complexity < 1 ? 1 : item.Complexity,
                                    EstimatedHours = item.EstimatedHours,
                                    OrderIndex = order++,
                                    IsBlocked = item.IsBlocked,
                                    BlockingReason = item.BlockingReason
                                });
                            }
                        }
                    }

                    scope.StageInsert(activityTask);
                    createdEntity = activityTask;
                    break;

                case CreationTaskTypeEnum.Risk:
                    var risk = new RiskEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        ProgramId = scopedProgramId,
                        ProjectId = scopedProjectId,
                        Level = targetLevel,
                        Title = request.Title.Trim(),
                        Description = request.Description?.Trim(),
                        Priority = request.Priority,
                        Status = request.Status,
                        StartDate = startDate,
                        DueDate = dueDate,
                        EndDate = dueDate,
                        AssigneeEmails = !string.IsNullOrWhiteSpace(request.RiskOwnerEmail) ? request.RiskOwnerEmail : request.AssigneeEmails?.Trim(),
                        Labels = request.Labels?.Trim(),
                        MilestoneId = request.MilestoneId,
                        Impact = request.Impact,
                        Probability = request.Probability,
                        MitigationStrategy = request.MitigationStrategy?.Trim(),
                        ContingencyPlan = request.ContingencyPlan?.Trim(),
                        RiskStatus = RiskStatusEnum.Identified,
                        ReporterEmail = _currentUser.Email
                    };

                    scope.StageInsert(risk);
                    createdEntity = risk;
                    break;

                case CreationTaskTypeEnum.Assumption:
                    var assumption = new AssumptionEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        ProgramId = scopedProgramId,
                        ProjectId = scopedProjectId,
                        Level = targetLevel,
                        Title = request.Title.Trim(),
                        Description = request.Description?.Trim(),
                        Priority = request.Priority,
                        Status = request.Status,
                        StartDate = startDate,
                        DueDate = dueDate,
                        EndDate = dueDate,
                        AssigneeEmails = !string.IsNullOrWhiteSpace(request.AssumptionOwnerEmail) ? request.AssumptionOwnerEmail : request.AssigneeEmails?.Trim(),
                        Labels = request.Labels?.Trim(),
                        MilestoneId = request.MilestoneId,
                        ValidationCriteria = request.ValidationCriteria?.Trim() ?? string.Empty,
                        ValidationStatus = request.ValidationStatus,
                        ImpactIfInvalid = request.Description?.Trim(),
                        ReporterEmail = _currentUser.Email
                    };

                    scope.StageInsert(assumption);
                    createdEntity = assumption;
                    break;

                case CreationTaskTypeEnum.Issue:
                    var issue = new IssueEntity
                    {
                        Id = Guid.CreateVersion7(),
                        TenantId = tenantId,
                        ProgramId = scopedProgramId,
                        ProjectId = scopedProjectId,
                        Level = targetLevel,
                        Title = request.Title.Trim(),
                        Description = request.Description?.Trim(),
                        Priority = request.Priority,
                        Severity = request.Severity,
                        Status = request.Status,
                        StartDate = startDate,
                        DueDate = dueDate,
                        EndDate = dueDate,
                        AssigneeEmails = !string.IsNullOrWhiteSpace(request.IssueOwnerEmail) ? request.IssueOwnerEmail : request.AssigneeEmails?.Trim(),
                        Labels = request.Labels?.Trim(),
                        MilestoneId = request.MilestoneId,
                        RootCause = request.RootCause?.Trim(),
                        Workaround = request.Workaround?.Trim(),
                        ReporterEmail = _currentUser.Email
                    };

                    scope.StageInsert(issue);
                    createdEntity = issue;
                    break;

                default:
                    return Result<BaseTaskEntity>.Failure($"Unsupported task creation type: {request.TaskType}");
            }

            return await Task.FromResult(Result<BaseTaskEntity>.Success(createdEntity));
        }, $"Create_{request.TaskType}_{request.Title}", ct);

        if (txResult.Succeeded && txResult.Data != null)
        {
            _logger.LogInformation("Successfully created {TaskType} '{Title}' ({Id}) in project {ProjectId}",
                request.TaskType, txResult.Data.Title, txResult.Data.Id, request.ProjectId);
        }

        return txResult;
    }

    public Task<Result<bool>> ValidateTaskDataAsync(TaskCreationRequestDto request, CancellationToken ct = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add("Title is required.");
        }
        else if (request.Title.Trim().Length > 255)
        {
            errors.Add("Title cannot exceed 255 characters.");
        }

        if (request.EstimatedHours.HasValue && request.EstimatedHours.Value < 0)
        {
            errors.Add("Estimated hours cannot be negative.");
        }

        if (request.StartDate.HasValue && request.DueDate.HasValue && request.DueDate.Value < request.StartDate.Value)
        {
            errors.Add("Due date cannot precede start date.");
        }

        switch (request.TaskType)
        {
            case CreationTaskTypeEnum.SprintTask:
                if (!request.SprintId.HasValue || request.SprintId.Value == Guid.Empty)
                {
                    errors.Add("A Sprint must be selected for Sprint Tasks.");
                }
                break;

            case CreationTaskTypeEnum.Risk:
                if (string.IsNullOrWhiteSpace(request.MitigationStrategy) || request.MitigationStrategy.Trim().Length < 10)
                {
                    errors.Add("Mitigation strategy is required (minimum 10 characters).");
                }
                break;

            case CreationTaskTypeEnum.Assumption:
                if (string.IsNullOrWhiteSpace(request.ValidationCriteria) || request.ValidationCriteria.Trim().Length < 10)
                {
                    errors.Add("Validation criteria is required (minimum 10 characters).");
                }
                break;

            case CreationTaskTypeEnum.Issue:
                if (string.IsNullOrWhiteSpace(request.RootCause) || request.RootCause.Trim().Length < 10)
                {
                    errors.Add("Root cause description is required (minimum 10 characters).");
                }
                break;
        }

        return Task.FromResult(errors.Count == 0
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(errors));
    }

    public async Task<TaskCreationRelatedDataDto> GetRelatedDataAsync(Guid projectId, CreationTaskTypeEnum taskType, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var result = new TaskCreationRelatedDataDto();

        if (projectId != Guid.Empty)
        {
            // Sprints
            result.Sprints = await db.Sprints
                .AsNoTracking()
                .Where(s => s.ProjectId == projectId && !s.IsDeleted)
                .OrderBy(s => s.SprintNumber)
                .Select(s => new SprintLookupItemDto(s.Id, s.SprintName, s.SprintNumber, s.Status == SprintStatusEnum.Completed))
                .ToListAsync(ct);

            // Project team members
            result.TeamMembers = await db.ProjectUserRoles
                .AsNoTracking()
                .Where(pur => pur.ProjectId == projectId && !pur.IsDeleted)
                .Include(pur => pur.User)
                .Where(pur => pur.User != null && !pur.User.IsDeleted && pur.User.IsActive)
                .Select(pur => new TeamMemberLookupItemDto(pur.User!.Email, pur.User.FullName, pur.Role.ToString()))
                .Distinct()
                .ToListAsync(ct);

            // Milestones from project calendar
            result.Milestones = await db.Set<CalendarEntity>()
                .AsNoTracking()
                .Where(c => c.ProjectId == projectId && !c.IsDeleted && c.EntryType == CalendarEntryTypeEnum.Milestone)
                .OrderBy(c => c.StartDate)
                .Select(c => new MilestoneLookupItemDto(c.Id, c.Title, c.EndDate ?? c.StartDate))
                .ToListAsync(ct);
        }

        return result;
    }

    public int CalculateRiskExposure(RiskProbabilityEnum probability, RiskImpactEnum impact)
    {
        return (int)probability * (int)impact;
    }

    public string GetTaskTypeDisplayName(CreationTaskTypeEnum taskType) => taskType switch
    {
        CreationTaskTypeEnum.SprintTask => "Sprint Task",
        CreationTaskTypeEnum.ActivityTask => "Activity Task",
        CreationTaskTypeEnum.Risk => "Risk",
        CreationTaskTypeEnum.Assumption => "Assumption",
        CreationTaskTypeEnum.Issue => "Issue",
        _ => "Task"
    };
}
