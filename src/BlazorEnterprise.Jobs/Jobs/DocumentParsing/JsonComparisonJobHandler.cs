using System.Text.Json;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.DocumentParsing;
using BlazorEnterprise.Core.Events.DocumentParsing;
using BlazorEnterprise.Jobs.Abstractions;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Jobs.Jobs.DocumentParsing;

/// <summary>
/// Background job handler for <see cref="JsonComparisonJobEvent"/>.
/// Invokes <see cref="IJsonComparisonService"/>, persists <see cref="JsonComparisonResultEntity"/>
/// using <see cref="IDbContextFactory{AppDbContext}"/>, and dispatches user notifications.
/// </summary>
public sealed class JsonComparisonJobHandler : IBatchJobHandler<JsonComparisonJobEvent>
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IJsonComparisonService _comparisonService;
    private readonly INotifyService _notifyService;
    private readonly ILogger<JsonComparisonJobHandler> _logger;

    public JsonComparisonJobHandler(
        IDbContextFactory<AppDbContext> dbFactory,
        IJsonComparisonService comparisonService,
        INotifyService notifyService,
        ILogger<JsonComparisonJobHandler> logger)
    {
        _dbFactory = dbFactory;
        _comparisonService = comparisonService;
        _notifyService = notifyService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(JsonComparisonJobEvent jobEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _comparisonService.CompareJsonAsync(jobEvent.SourceFileId, jobEvent.TargetFileId, cancellationToken);

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var entity = new JsonComparisonResultEntity
            {
                Id = result.ComparisonId,
                TenantId = jobEvent.TenantId,
                ProjectId = jobEvent.ProjectId,
                SourceFileId = result.SourceFileId,
                TargetFileId = result.TargetFileId,
                DiffResultJson = result.DiffResultJson,
                SummaryMetricsJson = JsonSerializer.Serialize(result.Summary)
            };

            db.JsonComparisonResults.Add(entity);
            db.IsTransactionScopeCommitAuthorized = true;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                db.IsTransactionScopeCommitAuthorized = false;
            }

            if (!string.IsNullOrWhiteSpace(jobEvent.SenderUserId))
            {
                await _notifyService.SendAsync(new BlazorEnterprise.Core.Dtos.SendNotificationReqDto
                {
                    UserId = jobEvent.SenderUserId,
                    Title = "JSON Comparison Completed",
                    Message = $"Comparison finished (+{result.Summary.Additions} / -{result.Summary.Deletions} / ~{result.Summary.Modifications}). Reference ID: '{jobEvent.CorrelationId}'.",
                    Severity = NotificationSeverityEnum.Info,
                    Category = NotificationCategoryEnum.Generic,
                    ProjectId = jobEvent.ProjectId ?? Guid.Empty
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JsonComparisonJobHandler failed for Source={SourceFileId}, Target={TargetFileId}. CorrelationId: {CorrelationId}",
                jobEvent.SourceFileId, jobEvent.TargetFileId, jobEvent.CorrelationId);
            throw;
        }
    }
}
