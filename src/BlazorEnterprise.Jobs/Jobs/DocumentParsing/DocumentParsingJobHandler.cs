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
/// Background job handler for <see cref="DocumentParsingJobEvent"/>.
/// Invokes <see cref="IDocumentParsingService"/>, persists <see cref="DocumentParsingResultEntity"/>
/// using <see cref="IDbContextFactory{AppDbContext}"/>, and dispatches user notifications.
/// </summary>
public sealed class DocumentParsingJobHandler : IBatchJobHandler<DocumentParsingJobEvent>
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDocumentParsingService _parsingService;
    private readonly INotifyService _notifyService;
    private readonly ILogger<DocumentParsingJobHandler> _logger;

    public DocumentParsingJobHandler(
        IDbContextFactory<AppDbContext> dbFactory,
        IDocumentParsingService parsingService,
        INotifyService notifyService,
        ILogger<DocumentParsingJobHandler> logger)
    {
        _dbFactory = dbFactory;
        _parsingService = parsingService;
        _notifyService = notifyService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(DocumentParsingJobEvent jobEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _parsingService.ParseDocumentAsync(jobEvent.ImportFileId, cancellationToken);

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var entity = new DocumentParsingResultEntity
            {
                Id = result.ResultId,
                TenantId = jobEvent.TenantId,
                ProjectId = jobEvent.ProjectId,
                ImportFileId = result.ImportFileId,
                DocumentType = result.DocumentType,
                ExtractedDataJson = result.ExtractedDataJson,
                ConfidenceScore = result.ConfidenceScore,
                Status = DocumentParsingStatusEnum.PendingReview
            };

            db.DocumentParsingResults.Add(entity);
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
                    Title = "Document Parsing Completed",
                    Message = $"Parsed document as '{result.DocumentType}' (Confidence: {result.ConfidenceScore:P0}). Reference ID: '{jobEvent.CorrelationId}'.",
                    Severity = NotificationSeverityEnum.Info,
                    Category = NotificationCategoryEnum.Generic,
                    ProjectId = jobEvent.ProjectId ?? Guid.Empty
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentParsingJobHandler failed for ImportFileId {ImportFileId}. CorrelationId: {CorrelationId}",
                jobEvent.ImportFileId, jobEvent.CorrelationId);
            throw;
        }
    }
}
