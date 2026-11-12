using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tasks.Validators;
using BlazorEnterprise.Core.Dtos.Tasks;
using BlazorEnterprise.Persistence.Context;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Implementation of <see cref="IActionItemService"/> providing management and metrics for task action items.
/// </summary>
public class ActionItemService : IActionItemService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ActionItemValidator _validator = new();

    public ActionItemService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ActionItemDto> CreateActionItemAsync(Guid taskId, CreateActionItemDto dto, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var parentTask = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? (BaseTaskEntity?)await db.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.Id == taskId, ct)
            ?? (BaseTaskEntity?)await db.Assumptions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == taskId, ct)
            ?? (BaseTaskEntity?)await db.Issues.AsNoTracking().FirstOrDefaultAsync(i => i.Id == taskId, ct)
            ?? throw new KeyNotFoundException($"Parent task with ID {taskId} was not found.");

        var maxOrder = await db.ActionItems
            .Where(a => a.ParentTaskId == taskId && a.ParentSubTaskId == null)
            .Select(a => (int?)a.OrderIndex)
            .MaxAsync(ct) ?? -1;

        var entity = new ActionItemEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = parentTask.TenantId,
            ProgramId = parentTask.ProgramId,
            ProjectId = parentTask.ProjectId,
            Level = parentTask.Level,
            ParentTaskId = taskId,
            ParentSubTaskId = null,
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status == 0 ? ActionItemStatusEnum.Open : dto.Status,
            Complexity = dto.Complexity < 1 ? 1 : dto.Complexity,
            EstimatedHours = dto.EstimatedHours,
            OrderIndex = maxOrder + 1,
            IsBlocked = dto.IsBlocked,
            BlockingReason = dto.BlockingReason
        };

        var validationResult = await _validator.ValidateAsync(entity, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        db.ActionItems.Add(entity);
        await db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<ActionItemDto> CreateActionItemUnderSubTaskAsync(Guid taskId, Guid subTaskId, CreateActionItemDto dto, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var subTask = await db.SubTasks
            .FirstOrDefaultAsync(s => s.Id == subTaskId, ct)
            ?? throw new KeyNotFoundException($"SubTask with ID {subTaskId} was not found.");

        if (subTask.ParentTaskId != taskId)
        {
            throw new InvalidOperationException($"SubTask {subTaskId} does not belong to Task {taskId}.");
        }

        var maxOrder = await db.ActionItems
            .Where(a => a.ParentSubTaskId == subTaskId)
            .Select(a => (int?)a.OrderIndex)
            .MaxAsync(ct) ?? -1;

        var entity = new ActionItemEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = subTask.TenantId,
            ProgramId = subTask.ProgramId,
            ProjectId = subTask.ProjectId,
            Level = subTask.Level,
            ParentTaskId = taskId,
            ParentSubTaskId = subTaskId,
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status == 0 ? ActionItemStatusEnum.Open : dto.Status,
            Complexity = dto.Complexity < 1 ? 1 : dto.Complexity,
            EstimatedHours = dto.EstimatedHours,
            OrderIndex = maxOrder + 1,
            IsBlocked = dto.IsBlocked,
            BlockingReason = dto.BlockingReason
        };

        var validationResult = await _validator.ValidateAsync(entity, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        db.ActionItems.Add(entity);
        await db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<ActionItemDto> UpdateActionItemAsync(Guid actionItemId, UpdateActionItemDto dto, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new KeyNotFoundException($"Action item {actionItemId} was not found.");

        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Status = dto.Status;
        entity.Complexity = dto.Complexity;
        entity.EstimatedHours = dto.EstimatedHours;
        entity.OrderIndex = dto.OrderIndex;
        entity.IsBlocked = dto.IsBlocked;
        entity.BlockingReason = dto.IsBlocked ? dto.BlockingReason : null;

        if (dto.ParentSubTaskId.HasValue && dto.ParentSubTaskId != entity.ParentSubTaskId)
        {
            var subTask = await db.SubTasks.FirstOrDefaultAsync(s => s.Id == dto.ParentSubTaskId.Value, ct)
                ?? throw new KeyNotFoundException($"Target SubTask {dto.ParentSubTaskId.Value} not found.");

            if (subTask.ParentTaskId != entity.ParentTaskId)
            {
                throw new InvalidOperationException($"Target SubTask does not belong to the same parent task.");
            }
            entity.ParentSubTaskId = dto.ParentSubTaskId;
        }

        var validationResult = await _validator.ValidateAsync(entity, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await db.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<ActionItemDto> MoveActionItemAsync(Guid actionItemId, Guid? newSubTaskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new KeyNotFoundException($"Action item {actionItemId} was not found.");

        if (newSubTaskId.HasValue)
        {
            var subTask = await db.SubTasks.FirstOrDefaultAsync(s => s.Id == newSubTaskId.Value, ct)
                ?? throw new KeyNotFoundException($"Target SubTask {newSubTaskId.Value} not found.");

            if (subTask.ParentTaskId != entity.ParentTaskId)
            {
                throw new InvalidOperationException($"Target SubTask does not belong to parent task {entity.ParentTaskId}.");
            }
        }

        entity.ParentSubTaskId = newSubTaskId;
        var maxOrder = await db.ActionItems
            .Where(a => a.ParentTaskId == entity.ParentTaskId && a.ParentSubTaskId == newSubTaskId && a.Id != actionItemId)
            .Select(a => (int?)a.OrderIndex)
            .MaxAsync(ct) ?? -1;

        entity.OrderIndex = maxOrder + 1;
        await db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<ActionItemDto> ReorderActionItemAsync(Guid actionItemId, int newOrderIndex, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new KeyNotFoundException($"Action item {actionItemId} was not found.");

        entity.OrderIndex = Math.Max(0, newOrderIndex);
        await db.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<ActionItemDto> UpdateStatusAsync(Guid actionItemId, ActionItemStatusEnum status, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new KeyNotFoundException($"Action item {actionItemId} was not found.");

        entity.Status = status;
        if (status == ActionItemStatusEnum.Completed && entity.IsBlocked)
        {
            entity.IsBlocked = false;
            entity.BlockingReason = null;
        }

        await db.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<ActionItemDto> ToggleBlockingAsync(Guid actionItemId, bool isBlocked, string? reason = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ActionItems
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new KeyNotFoundException($"Action item {actionItemId} was not found.");

        entity.IsBlocked = isBlocked;
        entity.BlockingReason = isBlocked ? reason : null;

        var validationResult = await _validator.ValidateAsync(entity, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await db.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<IReadOnlyList<ActionItemDto>> GetActionItemsByTaskAsync(Guid taskId, bool includeSubTaskItems = false, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ActionItems.AsNoTracking().Where(a => a.ParentTaskId == taskId);

        if (!includeSubTaskItems)
        {
            query = query.Where(a => a.ParentSubTaskId == null);
        }

        var items = await query.OrderBy(a => a.OrderIndex).ToListAsync(ct);
        return items.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ActionItemDto>> GetActionItemsBySubTaskAsync(Guid subTaskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var items = await db.ActionItems.AsNoTracking()
            .Where(a => a.ParentSubTaskId == subTaskId)
            .OrderBy(a => a.OrderIndex)
            .ToListAsync(ct);

        return items.Select(MapToDto).ToList();
    }

    public async Task<ActionItemDto?> GetActionItemAsync(Guid actionItemId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var item = await db.ActionItems.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct);

        return item == null ? null : MapToDto(item);
    }

    public async Task<IReadOnlyList<ActionItemDto>> GetBlockedItemsAsync(Guid taskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var items = await db.ActionItems.AsNoTracking()
            .Where(a => a.ParentTaskId == taskId && a.IsBlocked)
            .OrderBy(a => a.OrderIndex)
            .ToListAsync(ct);

        return items.Select(MapToDto).ToList();
    }

    public async Task DeleteActionItemAsync(Guid actionItemId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ActionItems.FirstOrDefaultAsync(a => a.Id == actionItemId, ct);
        if (entity != null)
        {
            db.ActionItems.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteActionItemsBySubTaskAsync(Guid subTaskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var items = await db.ActionItems.Where(a => a.ParentSubTaskId == subTaskId).ToListAsync(ct);
        if (items.Count > 0)
        {
            db.ActionItems.RemoveRange(items);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetActionItemCountAsync(Guid taskId, bool includeSubTasks = false, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ActionItems.AsNoTracking().Where(a => a.ParentTaskId == taskId);
        if (!includeSubTasks)
        {
            query = query.Where(a => a.ParentSubTaskId == null);
        }

        return await query.CountAsync(ct);
    }

    public async Task<decimal> GetCompletionPercentageAsync(Guid taskId, bool includeSubTasks = false, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ActionItems.AsNoTracking().Where(a => a.ParentTaskId == taskId);
        if (!includeSubTasks)
        {
            query = query.Where(a => a.ParentSubTaskId == null);
        }

        var total = await query.CountAsync(ct);
        if (total == 0)
        {
            return 0m;
        }

        var completed = await query.CountAsync(a => a.Status == ActionItemStatusEnum.Completed, ct);
        return Math.Round(((decimal)completed / total) * 100m, 2);
    }

    public async Task<int> GetBlockedCountAsync(Guid taskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ActionItems.AsNoTracking()
            .Where(a => a.ParentTaskId == taskId && a.IsBlocked)
            .CountAsync(ct);
    }

    public async Task<int> GetComplexityTotalAsync(Guid taskId, bool includeSubTasks = false, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ActionItems.AsNoTracking().Where(a => a.ParentTaskId == taskId);
        if (!includeSubTasks)
        {
            query = query.Where(a => a.ParentSubTaskId == null);
        }

        return await query.SumAsync(a => a.Complexity, ct);
    }

    private static ActionItemDto MapToDto(ActionItemEntity entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        ProjectId = entity.ProjectId,
        ParentTaskId = entity.ParentTaskId,
        ParentSubTaskId = entity.ParentSubTaskId,
        Title = entity.Title,
        Description = entity.Description,
        Status = entity.Status,
        Complexity = entity.Complexity,
        EstimatedHours = entity.EstimatedHours,
        OrderIndex = entity.OrderIndex,
        IsBlocked = entity.IsBlocked,
        BlockingReason = entity.BlockingReason,
        Created = entity.Created,
        CreatedBy = entity.CreatedBy
    };
}
