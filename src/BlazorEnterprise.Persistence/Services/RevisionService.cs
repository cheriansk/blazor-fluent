using System.Security.Claims;
using System.Text.Json;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Auditing;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Enterprise service implementing multi-draft scenario revisions, strictly monotonic VerNum allocation,
/// soft branch lineage (ParentVerNum, ParentRevisionHeaderId, ParentRowId), review workflows,
/// and single-active atomic promotion cascade.
/// Uses IDbContextFactory to prevent circuit concurrency crashes in Blazor Server (TG025).
/// </summary>
public class RevisionService : IRevisionService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RevisionService> _logger;

    public RevisionService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ILogger<RevisionService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<List<RevisionSummaryDto>> GetRevisionsAsync(
        TaskLevelEnum scopeLevel,
        VersionedFeatureTypeEnum featureType,
        Guid? programId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var tenantId = _tenantContext.TenantId;

        var query = context.RevisionHeaders
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId &&
                        r.ScopeLevel == scopeLevel &&
                        r.FeatureType == featureType);

        if (scopeLevel == TaskLevelEnum.Program || scopeLevel == TaskLevelEnum.Project)
        {
            query = query.Where(r => r.ProgramId == programId);
        }

        if (scopeLevel == TaskLevelEnum.Project)
        {
            query = query.Where(r => r.ProjectId == projectId);
        }

        var revisions = await query
            .OrderByDescending(r => r.VerNum)
            .ToListAsync(cancellationToken);

        var dtos = new List<RevisionSummaryDto>();
        foreach (var r in revisions)
        {
            int count = 0;
            if (featureType == VersionedFeatureTypeEnum.ProgramCalendar)
            {
                count = await context.ProgramCalendars
                    .AsNoTracking()
                    .CountAsync(c => c.RevisionHeaderId == r.Id, cancellationToken);
            }
            else if (featureType == VersionedFeatureTypeEnum.ProjectCalendar)
            {
                count = await context.ProjectCalendars
                    .AsNoTracking()
                    .CountAsync(c => c.RevisionHeaderId == r.Id, cancellationToken);
            }

            dtos.Add(new RevisionSummaryDto
            {
                Id = r.Id,
                ScopeLevel = r.ScopeLevel,
                ProgramId = r.ProgramId,
                ProjectId = r.ProjectId,
                FeatureType = r.FeatureType,
                VerNum = r.VerNum,
                ParentVerNum = r.ParentVerNum,
                ParentRevisionHeaderId = r.ParentRevisionHeaderId,
                RevisionName = r.RevisionName,
                Status = r.Status,
                CreatorEmail = r.CreatorEmail,
                AssignedToEmails = r.AssignedToEmails,
                ReviewerEmails = r.ReviewerEmails,
                ReviewComments = r.ReviewComments,
                Created = r.Created,
                SubmittedForReviewAtUtc = r.SubmittedForReviewAtUtc,
                ApprovedAtUtc = r.ApprovedAtUtc,
                ApprovedBy = r.ApprovedBy,
                ActivatedAtUtc = r.ActivatedAtUtc,
                ActivatedBy = r.ActivatedBy,
                SupersededAtUtc = r.SupersededAtUtc,
                CancelledAtUtc = r.CancelledAtUtc,
                CancellationReason = r.CancellationReason,
                RecordCount = count
            });
        }

        return dtos;
    }

    public async Task<RevisionHeaderEntity?> GetActiveRevisionAsync(
        TaskLevelEnum scopeLevel,
        VersionedFeatureTypeEnum featureType,
        Guid? programId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var tenantId = _tenantContext.TenantId;

        var query = context.RevisionHeaders
            .Where(r => r.TenantId == tenantId &&
                        r.ScopeLevel == scopeLevel &&
                        r.FeatureType == featureType &&
                        r.Status == RevisionStatusEnum.Active &&
                        !r.IsDeleted);

        if (scopeLevel == TaskLevelEnum.Program || scopeLevel == TaskLevelEnum.Project)
        {
            query = query.Where(r => r.ProgramId == programId);
        }

        if (scopeLevel == TaskLevelEnum.Project)
        {
            query = query.Where(r => r.ProjectId == projectId);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RevisionHeaderEntity> CreateDraftAsync(
        TaskLevelEnum scopeLevel,
        VersionedFeatureTypeEnum featureType,
        string revisionName,
        Guid? sourceRevisionHeaderId = null,
        Guid? programId = null,
        Guid? projectId = null,
        bool isBatch = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var tenantId = _tenantContext.TenantId;
        var now = _dateTimeProvider.Now.ToUniversalTime();
        var creator = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId;

        // Zero-Trust Scope & Hierarchy Ownership Validation
        if (scopeLevel == TaskLevelEnum.Project)
        {
            if (!projectId.HasValue || projectId.Value == Guid.Empty)
            {
                throw new ArgumentException("ProjectId is mandatory for Project-level scenario revisions.", nameof(projectId));
            }

            var project = await context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == projectId.Value && !p.IsDeleted, cancellationToken);

            if (project is null)
            {
                throw new KeyNotFoundException($"Project '{projectId.Value}' was not found in tenant '{tenantId}'.");
            }

            // Auto-align programId from the verified project hierarchy
            if (project.ProgramId.HasValue && project.ProgramId.Value != Guid.Empty)
            {
                programId = project.ProgramId.Value;
            }
            else if (!programId.HasValue || programId.Value == Guid.Empty)
            {
                throw new InvalidOperationException($"Project '{project.Name}' ({projectId.Value}) is not assigned to a parent Program, but ProgramId is required.");
            }
        }
        else if (scopeLevel == TaskLevelEnum.Program)
        {
            if (!programId.HasValue || programId.Value == Guid.Empty)
            {
                throw new ArgumentException("ProgramId is mandatory for Program-level scenario revisions.", nameof(programId));
            }
            projectId = null;
        }
        else if (scopeLevel == TaskLevelEnum.Tenant)
        {
            programId = null;
            projectId = null;
        }

        // 1. Strictly monotonic VerNum allocation across ALL statuses and soft-deleted rows using IgnoreQueryFilters()
        var scenarioQuery = context.RevisionHeaders
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId &&
                        r.ScopeLevel == scopeLevel &&
                        r.FeatureType == featureType);

        if (scopeLevel == TaskLevelEnum.Program || scopeLevel == TaskLevelEnum.Project)
        {
            scenarioQuery = scenarioQuery.Where(r => r.ProgramId == programId);
        }
        else
        {
            scenarioQuery = scenarioQuery.Where(r => r.ProgramId == null);
        }

        if (scopeLevel == TaskLevelEnum.Project)
        {
            scenarioQuery = scenarioQuery.Where(r => r.ProjectId == projectId);
        }
        else
        {
            scenarioQuery = scenarioQuery.Where(r => r.ProjectId == null);
        }

        var maxVerNum = await scenarioQuery.MaxAsync(r => (int?)r.VerNum, cancellationToken) ?? 0;
        var nextVerNum = maxVerNum + 1;

        // 2. Resolve source revision for cloning if specified or if an Active version exists
        RevisionHeaderEntity? sourceRevision = null;
        if (sourceRevisionHeaderId.HasValue && sourceRevisionHeaderId.Value != Guid.Empty)
        {
            sourceRevision = await context.RevisionHeaders
                .FirstOrDefaultAsync(r => r.Id == sourceRevisionHeaderId.Value, cancellationToken);
        }
        else
        {
            var activeQuery = context.RevisionHeaders
                .Where(r => r.TenantId == tenantId &&
                            r.ScopeLevel == scopeLevel &&
                            r.FeatureType == featureType &&
                            r.Status == RevisionStatusEnum.Active &&
                            !r.IsDeleted);

            if (scopeLevel == TaskLevelEnum.Program || scopeLevel == TaskLevelEnum.Project)
            {
                activeQuery = activeQuery.Where(r => r.ProgramId == programId);
            }

            if (scopeLevel == TaskLevelEnum.Project)
            {
                activeQuery = activeQuery.Where(r => r.ProjectId == projectId);
            }

            sourceRevision = await activeQuery.FirstOrDefaultAsync(cancellationToken);
        }

        var resolvedRevisionName = revisionName;
        if (string.IsNullOrWhiteSpace(resolvedRevisionName))
        {
            resolvedRevisionName = isBatch
                ? $"Batch Import - {now:yyyy-MM-dd HH:mm}"
                : $"Draft v{nextVerNum} ({now:yyyy-MM-dd})";
        }

        var draftHeader = new RevisionHeaderEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ScopeLevel = scopeLevel,
            ProgramId = programId,
            ProjectId = projectId,
            FeatureType = featureType,
            VerNum = nextVerNum,
            ParentVerNum = sourceRevision?.VerNum ?? -1,
            ParentRevisionHeaderId = sourceRevision?.Id ?? Guid.Empty,
            RevisionName = resolvedRevisionName,
            Status = RevisionStatusEnum.Draft,
            CreatorEmail = creator,
            AssignedToEmails = creator,
            Created = now,
            CreatedBy = creator,
            Updated = now,
            UpdatedBy = creator
        };

        context.RevisionHeaders.Add(draftHeader);

        // 3. Clone rows if source revision exists (UI mode only; batch mode populates from staged rows)
        int clonedCount = 0;
        if (sourceRevision is not null && !isBatch)
        {
            if (featureType == VersionedFeatureTypeEnum.ProgramCalendar && programId.HasValue)
            {
                var sourceRows = await context.ProgramCalendars
                    .AsNoTracking()
                    .Where(c => c.RevisionHeaderId == sourceRevision.Id && !c.IsDeleted)
                    .ToListAsync(cancellationToken);

                var oldIdToNewId = new Dictionary<Guid, Guid>();
                foreach (var row in sourceRows.Where(r => r.EntryType != CalendarEntryTypeEnum.Milestone))
                {
                    oldIdToNewId[row.Id] = Guid.CreateVersion7();
                }

                foreach (var sourceRow in sourceRows)
                {
                    var newId = oldIdToNewId.TryGetValue(sourceRow.Id, out var generatedId) ? generatedId : Guid.CreateVersion7();
                    Guid? mappedParentTimelineId = null;
                    if (sourceRow.ParentTimelineId.HasValue && oldIdToNewId.TryGetValue(sourceRow.ParentTimelineId.Value, out var newParentId))
                    {
                        mappedParentTimelineId = newParentId;
                    }

                    var clonedRow = new ProgramCalendarEntity
                    {
                        Id = newId,
                        TenantId = tenantId,
                        ProgramId = programId.Value,
                        RevisionHeaderId = draftHeader.Id,
                        RelId = sourceRow.RelId,
                        VerNum = nextVerNum,
                        ParentRowId = sourceRow.Id,
                        Title = sourceRow.Title,
                        Description = sourceRow.Description,
                        OwnerEmails = sourceRow.OwnerEmails,
                        ParentTimelineId = mappedParentTimelineId,
                        StartDate = sourceRow.StartDate,
                        EndDate = sourceRow.EndDate,
                        EntryType = sourceRow.EntryType,
                        Status = sourceRow.Status,
                        Created = now,
                        CreatedBy = creator,
                        Updated = now,
                        UpdatedBy = creator
                    };
                    context.ProgramCalendars.Add(clonedRow);
                    clonedCount++;
                }
            }
            else if (featureType == VersionedFeatureTypeEnum.ProjectCalendar && projectId.HasValue)
            {
                var sourceRows = await context.ProjectCalendars
                    .AsNoTracking()
                    .Where(c => c.RevisionHeaderId == sourceRevision.Id && !c.IsDeleted)
                    .ToListAsync(cancellationToken);

                var oldIdToNewId = new Dictionary<Guid, Guid>();
                foreach (var row in sourceRows.Where(r => r.EntryType != CalendarEntryTypeEnum.Milestone))
                {
                    oldIdToNewId[row.Id] = Guid.CreateVersion7();
                }

                foreach (var sourceRow in sourceRows)
                {
                    var newId = oldIdToNewId.TryGetValue(sourceRow.Id, out var generatedId) ? generatedId : Guid.CreateVersion7();
                    Guid? mappedParentTimelineId = null;
                    if (sourceRow.ParentTimelineId.HasValue && oldIdToNewId.TryGetValue(sourceRow.ParentTimelineId.Value, out var newParentId))
                    {
                        mappedParentTimelineId = newParentId;
                    }

                    var clonedRow = new ProjectCalendarEntity
                    {
                        Id = newId,
                        TenantId = tenantId,
                        ProjectId = projectId.Value,
                        ProgramId = sourceRow.ProgramId,
                        ParentProgramCalendarId = sourceRow.ParentProgramCalendarId,
                        RevisionHeaderId = draftHeader.Id,
                        RelId = sourceRow.RelId,
                        VerNum = nextVerNum,
                        ParentRowId = sourceRow.Id,
                        Title = sourceRow.Title,
                        Description = sourceRow.Description,
                        OwnerEmails = sourceRow.OwnerEmails,
                        ParentTimelineId = mappedParentTimelineId,
                        StartDate = sourceRow.StartDate,
                        EndDate = sourceRow.EndDate,
                        EntryType = sourceRow.EntryType,
                        Status = sourceRow.Status,
                        Created = now,
                        CreatedBy = creator,
                        Updated = now,
                        UpdatedBy = creator
                    };
                    context.ProjectCalendars.Add(clonedRow);
                    clonedCount++;
                }
            }
        }

        // 4. Record single summary AuditRecord for the draft creation
        var summaryAudit = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = _currentUser.UserId,
            UserEmail = creator,
            UserType = _tenantContext.UserType,
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = nameof(RevisionHeaderEntity),
            EntityId = draftHeader.Id.ToString(),
            Operation = EntityOperationEnum.Insert,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Action = "DraftCreated",
                DraftHeaderId = draftHeader.Id,
                VerNum = nextVerNum,
                RevisionName = resolvedRevisionName,
                ParentVerNum = draftHeader.ParentVerNum,
                ParentRevisionHeaderId = draftHeader.ParentRevisionHeaderId,
                ClonedRecordsCount = clonedCount
            }),
            Created = now,
            CreatedBy = creator,
            Updated = now,
            UpdatedBy = creator
        };
        context.AuditRecords.Add(summaryAudit);

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created Scenario Revision Draft v{VerNum} '{RevisionName}' (HeaderId: {HeaderId}) for Scope {Scope} Feature {Feature}. Cloned {Count} rows from v{ParentVerNum}.",
            nextVerNum, resolvedRevisionName, draftHeader.Id, scopeLevel, featureType, clonedCount, draftHeader.ParentVerNum);

        return draftHeader;
    }

    public async Task<RevisionHeaderEntity> SubmitForReviewAsync(
        Guid revisionHeaderId,
        string reviewerEmails,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var revision = await context.RevisionHeaders.FindAsync(new object[] { revisionHeaderId }, cancellationToken);
        if (revision is null)
        {
            throw new KeyNotFoundException($"RevisionHeader '{revisionHeaderId}' was not found.");
        }

        if (revision.Status != RevisionStatusEnum.Draft)
        {
            throw new InvalidOperationException($"Only revisions in Draft status can be submitted for review. Current status is '{revision.Status}'.");
        }

        var now = _dateTimeProvider.Now.ToUniversalTime();
        var actingUser = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId;

        revision.Status = RevisionStatusEnum.ReadyForReview;
        revision.ReviewerEmails = reviewerEmails;
        revision.AssignedToEmails = reviewerEmails;
        revision.SubmittedForReviewAtUtc = now;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            revision.ReviewComments = notes;
        }

        var audit = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = revision.TenantId,
            UserId = _currentUser.UserId,
            UserEmail = actingUser,
            UserType = _tenantContext.UserType,
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = nameof(RevisionHeaderEntity),
            EntityId = revision.Id.ToString(),
            Operation = EntityOperationEnum.Update,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Action = "SubmittedForReview",
                VerNum = revision.VerNum,
                ReviewerEmails = reviewerEmails,
                Notes = notes
            }),
            Created = now,
            CreatedBy = actingUser,
            Updated = now,
            UpdatedBy = actingUser
        };
        context.AuditRecords.Add(audit);

        await context.SaveChangesAsync(cancellationToken);
        return revision;
    }

    public async Task<RevisionHeaderEntity> ApproveRevisionAsync(
        Guid revisionHeaderId,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var revision = await context.RevisionHeaders.FindAsync(new object[] { revisionHeaderId }, cancellationToken);
        if (revision is null)
        {
            throw new KeyNotFoundException($"RevisionHeader '{revisionHeaderId}' was not found.");
        }

        if (revision.Status != RevisionStatusEnum.ReadyForReview)
        {
            throw new InvalidOperationException($"Only revisions in ReadyForReview status can be approved. Current status is '{revision.Status}'.");
        }

        var now = _dateTimeProvider.Now.ToUniversalTime();
        var actingUser = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId;

        revision.Status = RevisionStatusEnum.Approved;
        revision.ApprovedAtUtc = now;
        revision.ApprovedBy = actingUser;
        revision.AssignedToEmails = revision.CreatorEmail;
        if (!string.IsNullOrWhiteSpace(comments))
        {
            revision.ReviewComments = comments;
        }

        var audit = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = revision.TenantId,
            UserId = _currentUser.UserId,
            UserEmail = actingUser,
            UserType = _tenantContext.UserType,
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = nameof(RevisionHeaderEntity),
            EntityId = revision.Id.ToString(),
            Operation = EntityOperationEnum.Update,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Action = "RevisionApproved",
                VerNum = revision.VerNum,
                ApprovedBy = actingUser,
                Comments = comments
            }),
            Created = now,
            CreatedBy = actingUser,
            Updated = now,
            UpdatedBy = actingUser
        };
        context.AuditRecords.Add(audit);

        await context.SaveChangesAsync(cancellationToken);
        return revision;
    }

    public async Task<RevisionHeaderEntity> RejectRevisionAsync(
        Guid revisionHeaderId,
        string rejectionReason,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var revision = await context.RevisionHeaders.FindAsync(new object[] { revisionHeaderId }, cancellationToken);
        if (revision is null)
        {
            throw new KeyNotFoundException($"RevisionHeader '{revisionHeaderId}' was not found.");
        }

        if (revision.Status != RevisionStatusEnum.ReadyForReview)
        {
            throw new InvalidOperationException($"Only revisions in ReadyForReview status can be rejected. Current status is '{revision.Status}'.");
        }

        var now = _dateTimeProvider.Now.ToUniversalTime();
        var actingUser = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId;

        revision.Status = RevisionStatusEnum.Draft;
        revision.ReviewComments = $"Rejected: {rejectionReason}";
        revision.AssignedToEmails = revision.CreatorEmail;

        var audit = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = revision.TenantId,
            UserId = _currentUser.UserId,
            UserEmail = actingUser,
            UserType = _tenantContext.UserType,
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Warning,
            EntityName = nameof(RevisionHeaderEntity),
            EntityId = revision.Id.ToString(),
            Operation = EntityOperationEnum.Update,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Action = "RevisionRejected",
                VerNum = revision.VerNum,
                RejectedBy = actingUser,
                Reason = rejectionReason
            }),
            Created = now,
            CreatedBy = actingUser,
            Updated = now,
            UpdatedBy = actingUser
        };
        context.AuditRecords.Add(audit);

        await context.SaveChangesAsync(cancellationToken);
        return revision;
    }

    public async Task<RevisionHeaderEntity> ActivateRevisionAsync(
        Guid revisionHeaderId,
        string? activationNotes = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var revision = await context.RevisionHeaders.FindAsync(new object[] { revisionHeaderId }, cancellationToken);
        if (revision is null)
        {
            throw new KeyNotFoundException($"RevisionHeader '{revisionHeaderId}' was not found.");
        }

        if (revision.Status != RevisionStatusEnum.Approved && revision.Status != RevisionStatusEnum.Draft)
        {
            throw new InvalidOperationException($"Only revisions in Approved or Draft status can be activated. Current status is '{revision.Status}'.");
        }

        var now = _dateTimeProvider.Now.ToUniversalTime();
        var actingUser = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId;

        var activeQuery = context.RevisionHeaders
            .Where(r => r.TenantId == revision.TenantId &&
                        r.ScopeLevel == revision.ScopeLevel &&
                        r.FeatureType == revision.FeatureType &&
                        r.Status == RevisionStatusEnum.Active &&
                        !r.IsDeleted);

        if (revision.ScopeLevel == TaskLevelEnum.Program || revision.ScopeLevel == TaskLevelEnum.Project)
        {
            activeQuery = activeQuery.Where(r => r.ProgramId == revision.ProgramId);
        }

        if (revision.ScopeLevel == TaskLevelEnum.Project)
        {
            activeQuery = activeQuery.Where(r => r.ProjectId == revision.ProjectId);
        }

        var currentActive = await activeQuery.FirstOrDefaultAsync(cancellationToken);
        if (currentActive is not null)
        {
            currentActive.Status = RevisionStatusEnum.Superseded;
            currentActive.SupersededAtUtc = now;
        }

        var otherOpenQuery = context.RevisionHeaders
            .Where(r => r.TenantId == revision.TenantId &&
                        r.ScopeLevel == revision.ScopeLevel &&
                        r.FeatureType == revision.FeatureType &&
                        r.Id != revision.Id &&
                        (r.Status == RevisionStatusEnum.Draft ||
                         r.Status == RevisionStatusEnum.ReadyForReview ||
                         r.Status == RevisionStatusEnum.Approved) &&
                        !r.IsDeleted);

        if (revision.ScopeLevel == TaskLevelEnum.Program || revision.ScopeLevel == TaskLevelEnum.Project)
        {
            otherOpenQuery = otherOpenQuery.Where(r => r.ProgramId == revision.ProgramId);
        }

        if (revision.ScopeLevel == TaskLevelEnum.Project)
        {
            otherOpenQuery = otherOpenQuery.Where(r => r.ProjectId == revision.ProjectId);
        }

        var openRevisions = await otherOpenQuery.ToListAsync(cancellationToken);
        foreach (var openRev in openRevisions)
        {
            openRev.Status = RevisionStatusEnum.Cancelled;
            openRev.CancelledAtUtc = now;
            openRev.CancellationReason = $"Automatically cancelled upon activation of v{revision.VerNum} ({revision.RevisionName})";
        }

        revision.Status = RevisionStatusEnum.Active;
        revision.ActivatedAtUtc = now;
        revision.ActivatedBy = actingUser;

        var audit = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = revision.TenantId,
            UserId = _currentUser.UserId,
            UserEmail = actingUser,
            UserType = _tenantContext.UserType,
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = nameof(RevisionHeaderEntity),
            EntityId = revision.Id.ToString(),
            Operation = EntityOperationEnum.Update,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Action = "RevisionActivated",
                VerNum = revision.VerNum,
                PreviousActiveVerNum = currentActive?.VerNum,
                CancelledRevisionsCount = openRevisions.Count,
                Notes = activationNotes
            }),
            Created = now,
            CreatedBy = actingUser,
            Updated = now,
            UpdatedBy = actingUser
        };
        context.AuditRecords.Add(audit);

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Activated Scenario Revision v{VerNum} (HeaderId: {HeaderId}). Superseded v{PrevVer}. Cancelled {CancelledCount} competing draft(s).",
            revision.VerNum, revision.Id, currentActive?.VerNum, openRevisions.Count);

        return revision;
    }

    public async Task<RevisionHeaderEntity> DiscardDraftAsync(
        Guid revisionHeaderId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var revision = await context.RevisionHeaders.FindAsync(new object[] { revisionHeaderId }, cancellationToken);
        if (revision is null)
        {
            throw new KeyNotFoundException($"RevisionHeader '{revisionHeaderId}' was not found.");
        }

        if (revision.Status == RevisionStatusEnum.Active || revision.Status == RevisionStatusEnum.Superseded)
        {
            throw new InvalidOperationException($"Cannot discard an Active or Superseded revision. Current status is '{revision.Status}'.");
        }

        var now = _dateTimeProvider.Now.ToUniversalTime();
        var actingUser = !string.IsNullOrWhiteSpace(_currentUser.Email) ? _currentUser.Email : _currentUser.UserId;

        revision.Status = RevisionStatusEnum.Cancelled;
        revision.CancelledAtUtc = now;
        revision.CancellationReason = reason ?? "Manually discarded by user";

        var audit = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = revision.TenantId,
            UserId = _currentUser.UserId,
            UserEmail = actingUser,
            UserType = _tenantContext.UserType,
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = nameof(RevisionHeaderEntity),
            EntityId = revision.Id.ToString(),
            Operation = EntityOperationEnum.Update,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Action = "DraftDiscarded",
                VerNum = revision.VerNum,
                Reason = reason
            }),
            Created = now,
            CreatedBy = actingUser,
            Updated = now,
            UpdatedBy = actingUser
        };
        context.AuditRecords.Add(audit);

        await context.SaveChangesAsync(cancellationToken);
        return revision;
    }

    public async Task<RevisionComparisonDto> CompareRevisionsAsync(
        Guid leftRevisionHeaderId,
        Guid rightRevisionHeaderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var leftHeader = await context.RevisionHeaders.FindAsync(new object[] { leftRevisionHeaderId }, cancellationToken);
        var rightHeader = await context.RevisionHeaders.FindAsync(new object[] { rightRevisionHeaderId }, cancellationToken);

        if (leftHeader is null || rightHeader is null)
        {
            throw new KeyNotFoundException("One or both revision headers were not found.");
        }

        var result = new RevisionComparisonDto
        {
            LeftRevisionId = leftHeader.Id,
            LeftVerNum = leftHeader.VerNum,
            LeftRevisionName = leftHeader.RevisionName,
            LeftStatus = leftHeader.Status,
            RightRevisionId = rightHeader.Id,
            RightVerNum = rightHeader.VerNum,
            RightRevisionName = rightHeader.RevisionName,
            RightStatus = rightHeader.Status
        };

        if (leftHeader.FeatureType == VersionedFeatureTypeEnum.ProgramCalendar)
        {
            var leftRows = await context.ProgramCalendars
                .AsNoTracking()
                .Where(c => c.RevisionHeaderId == leftHeader.Id && !c.IsDeleted)
                .ToDictionaryAsync(c => c.RelId, cancellationToken);

            var rightRows = await context.ProgramCalendars
                .AsNoTracking()
                .Where(c => c.RevisionHeaderId == rightHeader.Id && !c.IsDeleted)
                .ToDictionaryAsync(c => c.RelId, cancellationToken);

            var allRelIds = leftRows.Keys.Union(rightRows.Keys);
            foreach (var relId in allRelIds)
            {
                leftRows.TryGetValue(relId, out var left);
                rightRows.TryGetValue(relId, out var right);

                if (left is null && right is not null)
                {
                    result.AddedRows.Add(new RevisionRowDiffDto
                    {
                        RelId = relId,
                        Title = right.Title,
                        EntryType = right.EntryType,
                        Status = right.Status,
                        RightStartDate = right.StartDate,
                        RightEndDate = right.EndDate,
                        Changes = new List<string> { "Added in right revision" }
                    });
                }
                else if (left is not null && right is null)
                {
                    result.RemovedRows.Add(new RevisionRowDiffDto
                    {
                        RelId = relId,
                        Title = left.Title,
                        EntryType = left.EntryType,
                        Status = left.Status,
                        LeftStartDate = left.StartDate,
                        LeftEndDate = left.EndDate,
                        Changes = new List<string> { "Removed from right revision" }
                    });
                }
                else if (left is not null && right is not null)
                {
                    var changes = new List<string>();
                    if (left.Title != right.Title) changes.Add($"Title: '{left.Title}' -> '{right.Title}'");
                    if (left.StartDate.Date != right.StartDate.Date) changes.Add($"StartDate: {left.StartDate:yyyy-MM-dd} -> {right.StartDate:yyyy-MM-dd}");
                    if (left.EndDate?.Date != right.EndDate?.Date) changes.Add($"EndDate: {left.EndDate:yyyy-MM-dd} -> {right.EndDate:yyyy-MM-dd}");
                    if (left.Status != right.Status) changes.Add($"Status: {left.Status} -> {right.Status}");
                    if (left.EntryType != right.EntryType) changes.Add($"EntryType: {left.EntryType} -> {right.EntryType}");

                    var diff = new RevisionRowDiffDto
                    {
                        RelId = relId,
                        Title = right.Title,
                        EntryType = right.EntryType,
                        Status = right.Status,
                        LeftStartDate = left.StartDate,
                        LeftEndDate = left.EndDate,
                        RightStartDate = right.StartDate,
                        RightEndDate = right.EndDate,
                        Changes = changes
                    };

                    if (changes.Count > 0)
                    {
                        result.ModifiedRows.Add(diff);
                    }
                    else
                    {
                        result.UnchangedRows.Add(diff);
                    }
                }
            }
        }
        else if (leftHeader.FeatureType == VersionedFeatureTypeEnum.ProjectCalendar)
        {
            var leftRows = await context.ProjectCalendars
                .AsNoTracking()
                .Where(c => c.RevisionHeaderId == leftHeader.Id && !c.IsDeleted)
                .ToDictionaryAsync(c => c.RelId, cancellationToken);

            var rightRows = await context.ProjectCalendars
                .AsNoTracking()
                .Where(c => c.RevisionHeaderId == rightHeader.Id && !c.IsDeleted)
                .ToDictionaryAsync(c => c.RelId, cancellationToken);

            var allRelIds = leftRows.Keys.Union(rightRows.Keys);
            foreach (var relId in allRelIds)
            {
                leftRows.TryGetValue(relId, out var left);
                rightRows.TryGetValue(relId, out var right);

                if (left is null && right is not null)
                {
                    result.AddedRows.Add(new RevisionRowDiffDto
                    {
                        RelId = relId,
                        Title = right.Title,
                        EntryType = right.EntryType,
                        Status = right.Status,
                        RightStartDate = right.StartDate,
                        RightEndDate = right.EndDate,
                        Changes = new List<string> { "Added in right revision" }
                    });
                }
                else if (left is not null && right is null)
                {
                    result.RemovedRows.Add(new RevisionRowDiffDto
                    {
                        RelId = relId,
                        Title = left.Title,
                        EntryType = left.EntryType,
                        Status = left.Status,
                        LeftStartDate = left.StartDate,
                        LeftEndDate = left.EndDate,
                        Changes = new List<string> { "Removed from right revision" }
                    });
                }
                else if (left is not null && right is not null)
                {
                    var changes = new List<string>();
                    if (left.Title != right.Title) changes.Add($"Title: '{left.Title}' -> '{right.Title}'");
                    if (left.StartDate.Date != right.StartDate.Date) changes.Add($"StartDate: {left.StartDate:yyyy-MM-dd} -> {right.StartDate:yyyy-MM-dd}");
                    if (left.EndDate?.Date != right.EndDate?.Date) changes.Add($"EndDate: {left.EndDate:yyyy-MM-dd} -> {right.EndDate:yyyy-MM-dd}");
                    if (left.Status != right.Status) changes.Add($"Status: {left.Status} -> {right.Status}");
                    if (left.EntryType != right.EntryType) changes.Add($"EntryType: {left.EntryType} -> {right.EntryType}");

                    var diff = new RevisionRowDiffDto
                    {
                        RelId = relId,
                        Title = right.Title,
                        EntryType = right.EntryType,
                        Status = right.Status,
                        LeftStartDate = left.StartDate,
                        LeftEndDate = left.EndDate,
                        RightStartDate = right.StartDate,
                        RightEndDate = right.EndDate,
                        Changes = changes
                    };

                    if (changes.Count > 0)
                    {
                        result.ModifiedRows.Add(diff);
                    }
                    else
                    {
                        result.UnchangedRows.Add(diff);
                    }
                }
            }
        }

        return result;
    }
}
