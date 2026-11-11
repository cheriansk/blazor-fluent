using System.Globalization;
using BlazorEnterprise.Core.Domain.Imports;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Events.Imports;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Jobs.FileImports.Committers;

/// <summary>
/// Domain committer for TaskEntity records produced by TaskCsv and TaskExcel batches.
/// </summary>
public sealed class TaskBatchDomainCommitter : IBatchDomainCommitter
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TaskBatchDomainCommitter> _logger;

    public TaskBatchDomainCommitter(
        AppDbContext dbContext,
        ILogger<TaskBatchDomainCommitter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public bool CanCommit(string targetEntityName) =>
        string.Equals(targetEntityName, "TaskEntity", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<int>> CommitProcessedRowsAsync(
        ImportFileEntity file,
        IReadOnlyList<ProcessedDomainRowDto> rows,
        FileImportBatchJobEvent jobEvent,
        CancellationToken ct = default)
    {
        if (file.TenantId != jobEvent.TenantId)
        {
            _logger.LogCritical("Zero-Trust Security Violation: File TenantId '{FileTenant}' does not match JobEvent TenantId '{JobTenant}'. Commit rejected.",
                file.TenantId, jobEvent.TenantId);
            return Result<int>.Failure("Zero-Trust Security Violation: Cross-tenant commit detected.");
        }

        var uploaderEmail = string.IsNullOrWhiteSpace(file.CreatedBy) ? "system@local" : file.CreatedBy;
        var committedCount = 0;

        foreach (var row in rows)
        {
            row.DomainProperties.TryGetValue("Title", out var title);
            row.DomainProperties.TryGetValue("Description", out var description);
            row.DomainProperties.TryGetValue("Status", out var statusStr);
            row.DomainProperties.TryGetValue("Priority", out var priorityStr);
            row.DomainProperties.TryGetValue("DueDateUtc", out var dueDateStr);

            var status = statusStr.ToEnum(BlazorEnterprise.Shared.DataListTypes.TaskStatusEnum.Open);
            var priority = priorityStr.ToEnum(TaskPriorityEnum.Medium);

            DateTime dueDateUtc = DateTime.UtcNow.AddDays(7);
            if (!string.IsNullOrWhiteSpace(dueDateStr) &&
                DateTime.TryParse(dueDateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDue))
            {
                dueDateUtc = parsedDue;
            }

            var taskEntity = new MiscTaskEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = file.TenantId,
                ProjectId = file.ProjectId,
                Level = TaskLevelEnum.Project,
                Title = string.IsNullOrWhiteSpace(title) ? "[ENCRYPTED]" : title,
                Description = description,
                Status = status,
                Priority = priority,
                StartDate = DateTime.UtcNow,
                DueDate = dueDateUtc,
                ReporterEmail = uploaderEmail,
                SourceRowId = row.SourceRowId,
                IsEncryptedAtRest = row.IsEncryptedAtRest,
                EncryptedPayloadBytes = row.EncryptedPayloadBytes
            };

            _dbContext.Tasks.Add(taskEntity);
            committedCount++;
        }

        _dbContext.IsTransactionScopeCommitAuthorized = true; try { await _dbContext.SaveChangesAsync(ct); } finally { _dbContext.IsTransactionScopeCommitAuthorized = false; }
        _logger.LogInformation("Committed {Count} TaskEntity records for File {FileId}.", committedCount, file.Id);
        return Result<int>.Success(committedCount);
    }
}
