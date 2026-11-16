using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.DocumentParsing;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Persistence service for reviewing, correcting, transitioning, and permanently finalizing
/// <see cref="DocumentParsingResultEntity"/> records with strict tenant isolation and fail-closed immutability.
/// </summary>
public sealed class DocumentParsingReviewService : IDocumentParsingReviewService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<DocumentParsingReviewService> _logger;

    public DocumentParsingReviewService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ILogger<DocumentParsingReviewService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentParsingReviewItemDto>> GetResultsAsync(Guid? projectId = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var query = db.DocumentParsingResults
            .AsNoTracking()
            .Include(r => r.ImportFile)
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        if (projectId.HasValue && projectId.Value != Guid.Empty)
        {
            query = query.Where(r => r.ProjectId == projectId.Value);
        }

        return await query
            .OrderByDescending(r => r.Created)
            .Select(r => new DocumentParsingReviewItemDto(
                r.Id,
                r.ImportFileId,
                r.ProjectId,
                r.ImportFile != null ? r.ImportFile.OriginalFileName : "Unknown.docx",
                r.DocumentType,
                r.ExtractedDataJson,
                r.ConfidenceScore,
                r.Status,
                r.Created,
                r.Updated,
                r.FinalizedAtUtc,
                r.FinalizedBy))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UpdateExtractedDataAsync(Guid resultId, string extractedDataJson, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var entity = await db.DocumentParsingResults
            .AsTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == resultId && !r.IsDeleted, ct);

        if (entity is null)
        {
            return Result<bool>.Failure("Parsed document result not found.");
        }

        if (entity.Status != DocumentParsingStatusEnum.PendingReview)
        {
            return Result<bool>.Failure($"Cannot edit record in '{entity.Status.GetDisplayName()}' status. Only Pending Review records can be edited.");
        }

        entity.ExtractedDataJson = extractedDataJson;
        db.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.IsTransactionScopeCommitAuthorized = false;
        }

        return Result<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ChangeStatusAsync(Guid resultId, DocumentParsingStatusEnum newStatus, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var entity = await db.DocumentParsingResults
            .AsTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == resultId && !r.IsDeleted, ct);

        if (entity is null)
        {
            return Result<bool>.Failure("Parsed document result not found.");
        }

        if (entity.Status == DocumentParsingStatusEnum.Finalized)
        {
            return Result<bool>.Failure("This record is Finalized and locked forever.");
        }

        if (!DocumentParsingStatusEnumDefinitions.AllowedTransitions.TryGetValue(entity.Status, out var allowed) || !allowed.Contains(newStatus))
        {
            return Result<bool>.Failure($"Transition from '{entity.Status.GetDisplayName()}' to '{newStatus.GetDisplayName()}' is not allowed.");
        }

        entity.Status = newStatus;
        if (newStatus == DocumentParsingStatusEnum.Finalized)
        {
            entity.FinalizedAtUtc = DateTime.UtcNow;
            entity.FinalizedBy = _currentUser.Email ?? _currentUser.UserId;
        }

        db.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.IsTransactionScopeCommitAuthorized = false;
        }

        return Result<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<Result<int>> FinalizeBatchAsync(IReadOnlyList<Guid> resultIds, CancellationToken ct = default)
    {
        if (resultIds.Count == 0) return Result<int>.Success(0);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var entities = await db.DocumentParsingResults
            .AsTracking()
            .Where(r => r.TenantId == tenantId && resultIds.Contains(r.Id) && !r.IsDeleted && r.Status != DocumentParsingStatusEnum.Finalized)
            .ToListAsync(ct);

        if (entities.Count == 0)
        {
            return Result<int>.Failure("No eligible non-finalized records selected.");
        }

        var now = DateTime.UtcNow;
        var user = _currentUser.Email ?? _currentUser.UserId;
        foreach (var entity in entities)
        {
            entity.Status = DocumentParsingStatusEnum.Finalized;
            entity.FinalizedAtUtc = now;
            entity.FinalizedBy = user;
        }

        db.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.IsTransactionScopeCommitAuthorized = false;
        }

        _logger.LogInformation("Finalized {Count} parsed document result(s) for Tenant {TenantId} by {User}.",
            entities.Count, tenantId, user);

        return Result<int>.Success(entities.Count);
    }
}
