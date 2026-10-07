using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Jobs.FileImports.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.FileImports.Tasks;

/// <summary>
/// Step 3 Domain Promotion: Promotes valid staged task records into live domain entities.
/// Inserts records into 'tasks.Tasks' (UserTaskEntity) and completes the file lifecycle.
/// </summary>
public class TaskProcess : IFileProcessor
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TaskProcess> _logger;

    public TaskProcess(AppDbContext dbContext, ILogger<TaskProcess> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public bool CanHandle(ImportFileType fileType) => fileType == ImportFileType.TaskCsv;

    public async Task<Result<int>> ProcessAsync(ImportFileEntity file, CancellationToken ct = default)
    {
        _logger.LogInformation("TaskProcess starting domain promotion for FileId={FileId} (Batch={ImportId})", file.Id, file.ImportId);

        // 1. Mark Processing
        file.Status = ImportStatus.Processing;
        file.StepStage = "Process";
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            // 2. Fetch staged tasks for this file
            var stagedTasks = await _dbContext.StagedTasks
                .Where(s => s.ImportFileId == file.Id && s.ValidationStatus == "Valid")
                .ToListAsync(ct);

            if (stagedTasks.Count == 0)
            {
                file.Status = ImportStatus.ProcessingFailed;
                file.ErrorMessage = "No valid staged records found to process into domain entities.";
                file.ProcessedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
                return Result<int>.Failure(file.ErrorMessage);
            }

            var liveTasks = new List<UserTaskEntity>(stagedTasks.Count);
            foreach (var staged in stagedTasks)
            {
                var priority = Enum.TryParse<UserTaskPriority>(staged.Priority, true, out var p) ? p : UserTaskPriority.Medium;
                var status = Enum.TryParse<UserTaskStatus>(staged.Status, true, out var s) ? s : UserTaskStatus.Open;

                var liveTask = new UserTaskEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = file.TenantId,
                    ProjectId = file.ProjectId,
                    Title = staged.Title,
                    Description = staged.Description,
                    Priority = priority,
                    Status = status,
                    DueDate = staged.DueDate ?? DateTime.UtcNow.AddDays(7),
                    ReporterEmail = !string.IsNullOrWhiteSpace(file.CreatedBy) ? file.CreatedBy : "import-batch",
                    AssigneeEmails = staged.AssigneeEmails ?? string.Empty,
                    Labels = staged.Labels ?? string.Empty
                };

                liveTasks.Add(liveTask);
            }

            _dbContext.Tasks.AddRange(liveTasks);

            // 3. Mark Completed
            file.Status = ImportStatus.Completed;
            file.StepStage = "Completed";
            file.ProcessedAtUtc = DateTime.UtcNow;
            file.ErrorMessage = null;

            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully committed {Count} live UserTaskEntity records for FileId={FileId}",
                liveTasks.Count, file.Id);

            return Result<int>.Success(liveTasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Domain promotion failed for FileId={FileId}", file.Id);
            file.Status = ImportStatus.ProcessingFailed;
            file.ErrorMessage = $"Domain processing failed: {ex.Message}";
            file.ProcessedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return Result<int>.Failure(file.ErrorMessage);
        }
    }
}
