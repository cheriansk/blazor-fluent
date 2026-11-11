using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Sprints;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

public class SprintPlanningService : ISprintPlanningService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IRevisionService _revisionService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SprintPlanningService> _logger;

    public SprintPlanningService(
        IDbContextFactory<AppDbContext> dbFactory,
        IRevisionService revisionService,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ILogger<SprintPlanningService> logger)
    {
        _dbFactory = dbFactory;
        _revisionService = revisionService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<List<SprintDto>> GetProjectSprintsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var sprints = await context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId && !s.IsDeleted)
            .OrderBy(s => s.SprintNumber)
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var result = new List<SprintDto>();

        foreach (var s in sprints)
        {
            var calculatedStatus = CalculateSprintStatus(s.StartDate, s.EndDate, now);
            var itemCount = await context.SprintItemVersions
                .AsNoTracking()
                .CountAsync(iv => iv.SprintId == s.Id && !iv.IsDeleted && iv.EffectiveToDate == null, cancellationToken);

            var totalHours = await context.SprintItemVersions
                .AsNoTracking()
                .Where(iv => iv.SprintId == s.Id && !iv.IsDeleted && iv.EffectiveToDate == null)
                .SumAsync(iv => (double?)iv.EstimatedHours, cancellationToken) ?? 0;

            result.Add(new SprintDto
            {
                Id = s.Id,
                ProjectId = s.ProjectId ?? Guid.Empty,
                SprintNumber = s.SprintNumber,
                SprintName = s.SprintName,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Comments = s.Comments,
                Status = calculatedStatus,
                ItemCount = itemCount,
                TotalPlannedHours = totalHours,
                Created = s.Created,
                CreatedBy = s.CreatedBy,
                Updated = s.Updated,
                UpdatedBy = s.UpdatedBy
            });
        }

        return result;
    }

    public async Task<SprintDto?> GetSprintByIdAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var s = await context.Sprints.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sprintId && !x.IsDeleted, cancellationToken);
        if (s == null) return null;

        var now = _dateTimeProvider.UtcNow;
        var itemCount = await context.SprintItemVersions
            .AsNoTracking()
            .CountAsync(iv => iv.SprintId == s.Id && !iv.IsDeleted && iv.EffectiveToDate == null, cancellationToken);

        var totalHours = await context.SprintItemVersions
            .AsNoTracking()
            .Where(iv => iv.SprintId == s.Id && !iv.IsDeleted && iv.EffectiveToDate == null)
            .SumAsync(iv => (double?)iv.EstimatedHours, cancellationToken) ?? 0;

        return new SprintDto
        {
            Id = s.Id,
            ProjectId = s.ProjectId ?? Guid.Empty,
            SprintNumber = s.SprintNumber,
            SprintName = s.SprintName,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            Comments = s.Comments,
            Status = CalculateSprintStatus(s.StartDate, s.EndDate, now),
            ItemCount = itemCount,
            TotalPlannedHours = totalHours,
            Created = s.Created,
            CreatedBy = s.CreatedBy,
            Updated = s.Updated,
            UpdatedBy = s.UpdatedBy
        };
    }

    public async Task<SprintEntity> CreateSprintAsync(
        Guid projectId,
        string sprintName,
        int sprintNumber,
        DateTime startDate,
        DateTime endDate,
        string? comments,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project == null) throw new InvalidOperationException("Project not found.");

        if (startDate < project.StartDate.Date || endDate > project.DueDate.Date || endDate <= startDate)
        {
            throw new InvalidOperationException(
                $"Sprint dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) must fall within the project timeline ({project.StartDate:yyyy-MM-dd} to {project.DueDate:yyyy-MM-dd}) and end date must be after start date.");
        }

        var overlappingSprint = await context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId && !s.IsDeleted)
            .FirstOrDefaultAsync(s => startDate < s.EndDate && s.StartDate < endDate, cancellationToken);

        if (overlappingSprint != null)
        {
            throw new InvalidOperationException(
                $"Sprint dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) overlap with existing Sprint {overlappingSprint.SprintNumber} '{overlappingSprint.SprintName}' ({overlappingSprint.StartDate:yyyy-MM-dd} to {overlappingSprint.EndDate:yyyy-MM-dd}). End date of a sprint may equal start date of another, but intervals cannot overlap.");
        }

        var existingNumber = await context.Sprints
            .AnyAsync(s => s.ProjectId == projectId && s.SprintNumber == sprintNumber && !s.IsDeleted, cancellationToken);
        if (existingNumber)
        {
            throw new InvalidOperationException($"Sprint number {sprintNumber} already exists in this project.");
        }

        var now = _dateTimeProvider.UtcNow;
        var sprint = new SprintEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantContext.TenantId,
            ProjectId = projectId,
            SprintNumber = sprintNumber,
            SprintName = sprintName,
            StartDate = startDate,
            EndDate = endDate,
            Comments = comments,
            Status = CalculateSprintStatus(startDate, endDate, now)
        };

        context.Sprints.Add(sprint);

        var audit = new SprintAuditLogEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantContext.TenantId,
            SprintId = sprint.Id,
            ChangeType = "Created",
            ChangedBy = _currentUser.UserId,
            ChangedDate = now,
            NewValue = $"Sprint {sprintNumber}: {sprintName}",
            Details = $"Created with dates {startDate:d} - {endDate:d}"
        };
        context.SprintAuditLogs.Add(audit);

        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
        return sprint;
    }

    public async Task UpdateSprintAsync(
        Guid sprintId,
        string sprintName,
        DateTime startDate,
        DateTime endDate,
        string? comments,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var sprint = await context.Sprints.FindAsync([sprintId], cancellationToken);
        if (sprint == null) throw new InvalidOperationException("Sprint not found.");

        var now = _dateTimeProvider.UtcNow;
        var currentStatus = CalculateSprintStatus(sprint.StartDate, sprint.EndDate, now);
        if (currentStatus == SprintStatusEnum.Completed && (sprint.StartDate != startDate || sprint.EndDate != endDate))
        {
            throw new InvalidOperationException("Cannot modify start or end dates of a completed sprint.");
        }

        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sprint.ProjectId, cancellationToken);
        if (project != null && (startDate < project.StartDate.Date || endDate > project.DueDate.Date || endDate <= startDate))
        {
            throw new InvalidOperationException(
                $"Sprint dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) must fall within the project timeline ({project.StartDate:yyyy-MM-dd} to {project.DueDate:yyyy-MM-dd}) and end date must be after start date.");
        }

        var overlappingSprint = await context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == sprint.ProjectId && !s.IsDeleted && s.Id != sprintId)
            .FirstOrDefaultAsync(s => startDate < s.EndDate && s.StartDate < endDate, cancellationToken);

        if (overlappingSprint != null)
        {
            throw new InvalidOperationException(
                $"Sprint dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) overlap with existing Sprint {overlappingSprint.SprintNumber} '{overlappingSprint.SprintName}' ({overlappingSprint.StartDate:yyyy-MM-dd} to {overlappingSprint.EndDate:yyyy-MM-dd}). End date of a sprint may equal start date of another, but intervals cannot overlap.");
        }

        var oldVal = $"Name: {sprint.SprintName}, Dates: {sprint.StartDate:d} - {sprint.EndDate:d}";
        sprint.SprintName = sprintName;
        sprint.StartDate = startDate;
        sprint.EndDate = endDate;
        sprint.Comments = comments;
        sprint.Status = CalculateSprintStatus(startDate, endDate, now);

        var newVal = $"Name: {sprintName}, Dates: {startDate:d} - {endDate:d}";
        var audit = new SprintAuditLogEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantContext.TenantId,
            SprintId = sprint.Id,
            ChangeType = "Modified",
            ChangedBy = _currentUser.UserId,
            ChangedDate = now,
            OldValue = oldVal,
            NewValue = newVal
        };
        context.SprintAuditLogs.Add(audit);

        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
    }

    public async Task DeleteSprintAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var sprint = await context.Sprints.FindAsync([sprintId], cancellationToken);
        if (sprint == null) return;

        var hasItems = await context.SprintItemVersions
            .AnyAsync(iv => iv.SprintId == sprintId && !iv.IsDeleted && iv.EffectiveToDate == null, cancellationToken);
        if (hasItems)
        {
            throw new InvalidOperationException("Cannot delete sprint because work items are currently assigned to it.");
        }

        var now = _dateTimeProvider.UtcNow;
        sprint.IsDeleted = true;
        sprint.DeletedAtUtc = now;
        sprint.DeletedBy = _currentUser.UserId;

        var audit = new SprintAuditLogEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantContext.TenantId,
            SprintId = sprint.Id,
            ChangeType = "Deleted",
            ChangedBy = _currentUser.UserId,
            ChangedDate = now,
            Details = $"Sprint {sprint.SprintNumber} ({sprint.SprintName}) deleted."
        };
        context.SprintAuditLogs.Add(audit);

        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
    }

    public async Task<SprintStatusEnum> GetSprintStatusAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var sprint = await context.Sprints.FindAsync([sprintId], cancellationToken);
        if (sprint == null) return SprintStatusEnum.Planned;
        return CalculateSprintStatus(sprint.StartDate, sprint.EndDate, _dateTimeProvider.UtcNow);
    }

    public async Task<SprintMetricsDto> GetSprintMetricsAsync(Guid? sprintId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (sprintId.HasValue)
        {
            var sprint = await context.Sprints.FindAsync([sprintId.Value], cancellationToken);
            if (sprint == null) throw new InvalidOperationException("Sprint not found.");

            var items = await context.SprintItemVersions
                .AsNoTracking()
                .Include(iv => iv.Item)
                .Where(iv => iv.SprintId == sprintId.Value && !iv.IsDeleted && iv.EffectiveToDate == null)
                .ToListAsync(cancellationToken);

            var planned = items.Sum(i => i.EstimatedHours);
            var completed = items.Where(i => i.Status == TaskStatusEnum.Closed).Sum(i => i.EstimatedHours);
            var blocked = items.Where(i => i.Status == TaskStatusEnum.Paused).Sum(i => i.EstimatedHours);
            var remaining = Math.Max(0, planned - completed);
            var pct = planned > 0 ? (completed / planned) * 100.0 : 0.0;

            return new SprintMetricsDto
            {
                SprintId = sprint.Id,
                SprintName = sprint.SprintName,
                SprintNumber = sprint.SprintNumber,
                Status = CalculateSprintStatus(sprint.StartDate, sprint.EndDate, now),
                StartDate = sprint.StartDate,
                EndDate = sprint.EndDate,
                PlannedHours = planned,
                CompletedHours = completed,
                BlockedHours = blocked,
                RemainingHours = remaining,
                CompletionPercentage = Math.Round(pct, 1),
                TotalItemCount = items.Count
            };
        }
        else
        {
            // Backlog (unallocated items)
            var backlogItems = await context.Tasks
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            var activeAssignedItemIds = await context.SprintItemVersions
                .AsNoTracking()
                .Where(iv => iv.ProjectId == projectId && !iv.IsDeleted && iv.EffectiveToDate == null && iv.SprintId != null)
                .Select(iv => iv.ItemId)
                .ToListAsync(cancellationToken);

            var unassigned = backlogItems.Where(b => !activeAssignedItemIds.Contains(b.Id)).ToList();
            var planned = 0.0;

            return new SprintMetricsDto
            {
                SprintId = Guid.Empty,
                SprintName = "Backlog",
                SprintNumber = 0,
                Status = SprintStatusEnum.Planned,
                StartDate = now,
                EndDate = now.AddDays(14),
                PlannedHours = planned,
                CompletedHours = 0,
                BlockedHours = 0,
                RemainingHours = planned,
                CompletionPercentage = 0,
                TotalItemCount = unassigned.Count
            };
        }
    }

    public async Task<List<SprintItemDto>> GetSprintItemsAsync(Guid? sprintId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (sprintId.HasValue)
        {
            var sprint = await context.Sprints.FindAsync([sprintId.Value], cancellationToken);
            var items = await context.SprintItemVersions
                .AsNoTracking()
                .Include(iv => iv.Item)
                .Where(iv => iv.SprintId == sprintId.Value && !iv.IsDeleted && iv.EffectiveToDate == null)
                .OrderBy(iv => iv.Item!.Title)
                .ToListAsync(cancellationToken);

            return items.Select(iv => new SprintItemDto
            {
                Id = iv.Id,
                ItemId = iv.ItemId,
                Title = iv.Item?.Title ?? "Untitled Task",
                SprintId = sprint?.Id,
                SprintName = sprint?.SprintName,
                Status = iv.Status,
                AssignedToName = iv.Item?.AssigneeEmails ?? "Unassigned",
                EstimatedHours = iv.EstimatedHours,
                ActualHours = iv.ActualHours,
                StartDate = iv.EffectiveFromDate,
                EndDate = iv.EffectiveToDate
            }).ToList();
        }
        else
        {
            // Backlog
            var allTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .OrderBy(t => t.Title)
                .ToListAsync(cancellationToken);

            var assignedItemIds = await context.SprintItemVersions
                .AsNoTracking()
                .Where(iv => iv.ProjectId == projectId && !iv.IsDeleted && iv.EffectiveToDate == null && iv.SprintId != null)
                .Select(iv => iv.ItemId)
                .ToListAsync(cancellationToken);

            return allTasks.Where(t => !assignedItemIds.Contains(t.Id)).Select(t => new SprintItemDto
            {
                Id = Guid.Empty,
                ItemId = t.Id,
                Title = t.Title,
                SprintId = null,
                SprintName = "Backlog",
                Status = t.Status,
                AssignedToName = t.AssigneeEmails ?? "Unassigned",
                EstimatedHours = 0,
                ActualHours = 0,
                StartDate = t.StartDate,
                EndDate = t.DueDate
            }).ToList();
        }
    }

    public async Task<List<SprintItemDto>> GetItemsReadyForSprintAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var allTasks = await context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && !t.IsDeleted)
            .OrderBy(t => t.Title)
            .ToListAsync(cancellationToken);

        var assignedVersions = await context.SprintItemVersions
            .AsNoTracking()
            .Include(iv => iv.Sprint)
            .Where(iv => iv.ProjectId == projectId && !iv.IsDeleted && iv.EffectiveToDate == null)
            .ToDictionaryAsync(iv => iv.ItemId, cancellationToken);

        return allTasks.Select(t =>
        {
            assignedVersions.TryGetValue(t.Id, out var assigned);
            return new SprintItemDto
            {
                Id = assigned?.Id ?? Guid.Empty,
                ItemId = t.Id,
                Title = t.Title,
                SprintId = assigned?.SprintId,
                SprintName = assigned?.Sprint?.SprintName ?? "Backlog",
                Status = assigned?.Status ?? t.Status,
                AssignedToName = t.AssigneeEmails ?? "Unassigned",
                EstimatedHours = assigned?.EstimatedHours ?? 0,
                ActualHours = assigned?.ActualHours ?? 0,
                StartDate = t.StartDate,
                EndDate = t.DueDate
            };
        }).ToList();
    }

    public async Task MoveItemsToSprintAsync(
        List<Guid> itemIds,
        Guid? targetSprintId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (targetSprintId.HasValue)
        {
            var targetSprint = await context.Sprints.FindAsync([targetSprintId.Value], cancellationToken);
            if (targetSprint == null) throw new InvalidOperationException("Target sprint not found.");
            if (CalculateSprintStatus(targetSprint.StartDate, targetSprint.EndDate, now) == SprintStatusEnum.Completed)
            {
                throw new InvalidOperationException("Cannot move items to a completed sprint.");
            }
        }

        // Get or create sprint plan revision header
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        var activeHeader = await _revisionService.GetActiveRevisionAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.SprintPlan,
            programId: project?.ProgramId,
            projectId: projectId,
            cancellationToken: cancellationToken);

        Guid revisionHeaderId;
        if (activeHeader != null)
        {
            revisionHeaderId = activeHeader.Id;
        }
        else
        {
            var revisions = await _revisionService.GetRevisionsAsync(
                TaskLevelEnum.Project,
                VersionedFeatureTypeEnum.SprintPlan,
                programId: project?.ProgramId,
                projectId: projectId,
                cancellationToken: cancellationToken);

            var existingDraft = revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Draft);
            if (existingDraft != null)
            {
                revisionHeaderId = existingDraft.Id;
            }
            else
            {
                var newDraft = await _revisionService.CreateDraftAsync(
                    TaskLevelEnum.Project,
                    VersionedFeatureTypeEnum.SprintPlan,
                    "Sprint Plan v1",
                    programId: project?.ProgramId,
                    projectId: projectId,
                    cancellationToken: cancellationToken);
                revisionHeaderId = newDraft.Id;
            }
        }

        foreach (var itemId in itemIds)
        {
            var currentVersion = await context.SprintItemVersions
                .Include(iv => iv.Sprint)
                .FirstOrDefaultAsync(iv => iv.ItemId == itemId && !iv.IsDeleted && iv.EffectiveToDate == null, cancellationToken);

            if (currentVersion?.Sprint != null)
            {
                var sourceStatus = CalculateSprintStatus(currentVersion.Sprint.StartDate, currentVersion.Sprint.EndDate, now);
                if (sourceStatus == SprintStatusEnum.Completed)
                {
                    throw new InvalidOperationException($"Cannot move item '{itemId}' out of completed sprint '{currentVersion.Sprint.SprintName}'.");
                }
            }

            var task = await context.Tasks.FindAsync([itemId], cancellationToken);
            if (task != null)
            {
                if (targetSprintId.HasValue)
                {
                    if (task is SprintTaskEntity st)
                    {
                        st.SprintId = targetSprintId.Value;
                    }
                    else
                    {
                        // Promote MiscTaskEntity to SprintTaskEntity
                        var sprintTask = new SprintTaskEntity
                        {
                            Id = task.Id,
                            TenantId = task.TenantId,
                            Level = task.Level,
                            Title = task.Title,
                            Description = task.Description,
                            Priority = task.Priority,
                            Status = task.Status,
                            StartDate = task.StartDate,
                            DueDate = task.DueDate,
                            ReporterEmail = task.ReporterEmail,
                            AssigneeEmails = task.AssigneeEmails,
                            WatcherEmails = task.WatcherEmails,
                            Labels = task.Labels,
                            EstimatedHours = task.EstimatedHours,
                            SprintId = targetSprintId.Value
                        };
                        context.Tasks.Remove(task);
                        context.SprintTasks.Add(sprintTask);
                    }
                }
                else
                {
                    // Moving to backlog (null sprint) -> ensure it is a MiscTaskEntity
                    if (task is SprintTaskEntity st)
                    {
                        var miscTask = new MiscTaskEntity
                        {
                            Id = task.Id,
                            TenantId = task.TenantId,
                            Level = task.Level,
                            Title = task.Title,
                            Description = task.Description,
                            Priority = task.Priority,
                            Status = task.Status,
                            StartDate = task.StartDate,
                            DueDate = task.DueDate,
                            ReporterEmail = task.ReporterEmail,
                            AssigneeEmails = task.AssigneeEmails,
                            WatcherEmails = task.WatcherEmails,
                            Labels = task.Labels,
                            EstimatedHours = task.EstimatedHours
                        };
                        context.Tasks.Remove(task);
                        context.MiscTasks.Add(miscTask);
                    }
                }
            }

            var estimatedHours = currentVersion?.EstimatedHours ?? 0;
            var actualHours = currentVersion?.ActualHours ?? 0;
            var status = currentVersion?.Status ?? task?.Status ?? TaskStatusEnum.Open;

            if (currentVersion != null)
            {
                currentVersion.EffectiveToDate = now;
            }

            var newVersion = new SprintItemVersionEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = _tenantContext.TenantId,
                ProjectId = projectId,
                ItemId = itemId,
                SprintId = targetSprintId,
                EstimatedHours = estimatedHours,
                ActualHours = actualHours,
                Status = status,
                EffectiveFromDate = now,
                EffectiveToDate = null,
                RevisionHeaderId = revisionHeaderId,
                RelId = currentVersion?.RelId ?? Guid.CreateVersion7(),
                VerNum = (currentVersion?.VerNum ?? 0) + 1,
                ParentRowId = currentVersion?.Id ?? Guid.Empty
            };
            context.SprintItemVersions.Add(newVersion);
        }

        context.IsTransactionScopeCommitAuthorized = true; try { await context.SaveChangesAsync(cancellationToken); } finally { context.IsTransactionScopeCommitAuthorized = false; }
    }

    public async Task<List<ResourceCapacityDto>> CalculateResourceCapacityAsync(
        Guid projectId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var resources = await context.ProjectResources
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderBy(r => r.User!.FullName)
            .ToListAsync(cancellationToken);

        var sprintItems = await context.SprintItemVersions
            .AsNoTracking()
            .Include(iv => iv.Item)
            .Include(iv => iv.Sprint)
            .Where(iv => iv.ProjectId == projectId && !iv.IsDeleted && iv.EffectiveToDate == null && iv.SprintId != null)
            .ToListAsync(cancellationToken);

        var days = Math.Max(1, (int)(endDate.Date - startDate.Date).TotalDays + 1);
        var standardWorkHours = days * 8.0;

        return resources.Select(r =>
        {
            var allocated = sprintItems
                .Where(si => si.Item?.AssigneeEmails == r.UserName || si.Item?.AssigneeEmails == r.UserEmail)
                .Sum(si => si.EstimatedHours);

            return new ResourceCapacityDto
            {
                ResourceId = r.Id,
                ResourceName = r.UserName,
                Role = r.Role,
                AvailableCapacityHours = standardWorkHours,
                AllocatedHours = allocated
            };
        }).ToList();
    }

    public async Task<bool> ValidateSprintDatesAsync(
        Guid projectId,
        DateTime startDate,
        DateTime endDate,
        Guid? excludeSprintId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project == null) return false;

        if (startDate < project.StartDate.Date || endDate > project.DueDate.Date || endDate <= startDate)
        {
            return false;
        }

        var hasOverlap = await context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId && !s.IsDeleted && (excludeSprintId == null || s.Id != excludeSprintId))
            .AnyAsync(s => startDate < s.EndDate && s.StartDate < endDate, cancellationToken);

        return !hasOverlap;
    }

    public async Task<List<SprintAuditLogEntity>> GetSprintAuditHistoryAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await context.SprintAuditLogs
            .AsNoTracking()
            .Where(a => a.SprintId == sprintId)
            .OrderByDescending(a => a.ChangedDate)
            .ToListAsync(cancellationToken);
    }

    private static SprintStatusEnum CalculateSprintStatus(DateTime startDate, DateTime endDate, DateTime now)
    {
        if (now < startDate) return SprintStatusEnum.Planned;
        if (now > endDate) return SprintStatusEnum.Completed;
        return SprintStatusEnum.Active;
    }
}
