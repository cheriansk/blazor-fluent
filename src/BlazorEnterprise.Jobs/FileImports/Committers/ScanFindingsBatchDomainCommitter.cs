using BlazorEnterprise.Core.Domain.DocumentParsing;
using BlazorEnterprise.Core.Domain.Imports;
using BlazorEnterprise.Core.Events.Imports;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Jobs.FileImports.Committers;

/// <summary>
/// Commits Stage 2b <c>ScanFinding</c> rows from <see cref="ProcessedDomainRowDto"/> into <c>app.ScanFindings</c>.
/// </summary>
public sealed class ScanFindingsBatchDomainCommitter : IBatchDomainCommitter
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<ScanFindingsBatchDomainCommitter> _logger;

    public ScanFindingsBatchDomainCommitter(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<ScanFindingsBatchDomainCommitter> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public bool CanCommit(string targetEntityName) =>
        string.Equals(targetEntityName, "ScanFinding", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<int>> CommitProcessedRowsAsync(
        ImportFileEntity file,
        IReadOnlyList<ProcessedDomainRowDto> rows,
        FileImportBatchJobEvent jobEvent,
        CancellationToken ct = default)
    {
        if (rows.Count == 0)
        {
            return Result<int>.Success(0);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entities = new List<ScanFindingEntity>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var props = rows[i].DomainProperties;
            if (props is null)
            {
                continue;
            }

            props.TryGetValue("Description", out var description);
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            props.TryGetValue("DocumentType", out var docType);
            props.TryGetValue("CustomerName", out var customerName);
            props.TryGetValue("SourceVersion", out var sourceVersion);
            props.TryGetValue("TargetVersion", out var targetVersion);
            props.TryGetValue("ProjectManager", out var projectManager);
            props.TryGetValue("Comment", out var comment);
            props.TryGetValue("OrderIndex", out var orderIndexRaw);

            entities.Add(new ScanFindingEntity
            {
                TenantId = file.TenantId,
                ProjectId = file.ProjectId,
                ImportFileId = file.Id,
                DocumentType = !string.IsNullOrWhiteSpace(docType) ? docType : file.FileType.ToString(),
                CustomerName = customerName,
                SourceVersion = sourceVersion,
                TargetVersion = targetVersion,
                ProjectManager = projectManager,
                Description = description.Trim(),
                Comment = comment,
                OrderIndex = int.TryParse(orderIndexRaw, out var idx) ? idx : i + 1
            });
        }

        if (entities.Count == 0)
        {
            return Result<int>.Success(0);
        }

        db.ScanFindings.AddRange(entities);
        db.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.IsTransactionScopeCommitAuthorized = false;
        }

        _logger.LogInformation(
            "Committed {Count} ScanFindingEntity rows for ImportFileId {ImportFileId}.",
            entities.Count,
            file.Id);

        return Result<int>.Success(entities.Count);
    }
}
