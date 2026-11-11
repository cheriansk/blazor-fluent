using System.Globalization;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Imports;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Events.Imports;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Jobs.FileImports.Committers;

/// <summary>
/// Domain committer for CalendarEntity and CalendarEntity records.
/// Reconstructs the Timeline -> Milestone hierarchy from API-validated rows and delegates
/// directly to ICalendarService and IRevisionService for draft creation and lineage persistence.
/// </summary>
public sealed class CalendarBatchDomainCommitter : IBatchDomainCommitter
{
    private readonly ICalendarService _calendarService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CalendarBatchDomainCommitter> _logger;

    public CalendarBatchDomainCommitter(
        ICalendarService calendarService,
        AppDbContext dbContext,
        ILogger<CalendarBatchDomainCommitter> logger)
    {
        _calendarService = calendarService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public bool CanCommit(string targetEntityName) =>
        string.Equals(targetEntityName, "CalendarEntity", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetEntityName, "CalendarEntity", StringComparison.OrdinalIgnoreCase);

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

        var isProgram = rows.Any(r => string.Equals(r.TargetEntityName, "CalendarEntity", StringComparison.OrdinalIgnoreCase));
        var batchRevisionName = $"Batch Import - {file.OriginalFileName} ({DateTime.UtcNow:yyyy-MM-dd HH:mm})";

        if (isProgram)
        {
            // Resolve ProgramId from ProgramCode on rows or Project.ProgramId
            Guid resolvedProgramId = Guid.Empty;
            var explicitCode = rows.FirstOrDefault(r => r.DomainProperties.TryGetValue("ProgramCode", out var code) && !string.IsNullOrWhiteSpace(code))
                ?.DomainProperties["ProgramCode"];

            if (!string.IsNullOrWhiteSpace(explicitCode))
            {
                var program = await _dbContext.Programs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TenantId == file.TenantId && p.Code == explicitCode && !p.IsDeleted, ct);
                if (program != null)
                {
                    resolvedProgramId = program.Id;
                }
            }

            if (resolvedProgramId == Guid.Empty)
            {
                var project = await _dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TenantId == file.TenantId && p.Id == file.ProjectId && !p.IsDeleted, ct);
                resolvedProgramId = project?.ProgramId ?? Guid.Empty;
            }

            if (resolvedProgramId == Guid.Empty)
            {
                _logger.LogError("Unable to resolve ProgramId for ProgramCalendar batch import. FileId={FileId}.", file.Id);
                return Result<int>.Failure("Unable to resolve target Program for ProgramCalendar batch. Specify ProgramCode or ensure the active Project is linked to a Program.");
            }

            // Reconstruct ProgramTimelineRowDto list
            var timelineRows = BuildProgramTimelineRows(rows);

            return await _calendarService.SaveProgramCalendarScheduleAsync(
                tenantId: file.TenantId,
                programId: resolvedProgramId,
                timelineRows: timelineRows,
                revisionHeaderId: null,
                isBatchDraft: true,
                batchRevisionName: batchRevisionName,
                ct: ct);
        }
        else
        {
            // Project Calendar
            var project = await _dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == file.TenantId && p.Id == file.ProjectId && !p.IsDeleted, ct);

            if (project == null)
            {
                _logger.LogError("Project {ProjectId} not found for ProjectCalendar import. FileId={FileId}.", file.ProjectId, file.Id);
                return Result<int>.Failure($"Project '{file.ProjectId}' was not found in tenant '{file.TenantId}'.");
            }

            var phases = BuildProjectPhases(rows);

            return await _calendarService.SaveProjectCalendarScheduleAsync(
                tenantId: file.TenantId,
                projectId: project.Id,
                programId: project.ProgramId,
                phases: phases,
                revisionHeaderId: null,
                isBatchDraft: true,
                batchRevisionName: batchRevisionName,
                ct: ct);
        }
    }

    private static List<ProgramTimelineRowDto> BuildProgramTimelineRows(IReadOnlyList<ProcessedDomainRowDto> rows)
    {
        var timelines = new List<ProgramTimelineRowDto>();
        var timelineMap = new Dictionary<Guid, ProgramTimelineRowDto>();

        foreach (var r in rows)
        {
            r.DomainProperties.TryGetValue("EntryType", out var entryType);
            r.DomainProperties.TryGetValue("Title", out var title);
            r.DomainProperties.TryGetValue("StartDateUtc", out var startStr);
            r.DomainProperties.TryGetValue("EndDateUtc", out var endStr);
            r.DomainProperties.TryGetValue("Status", out var status);
            r.DomainProperties.TryGetValue("Comments", out var comments);

            DateTime? start = DateTime.TryParse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var s) ? s : null;
            DateTime? end = DateTime.TryParse(endStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var e) ? e : null;

            if (string.Equals(entryType, "Timeline", StringComparison.OrdinalIgnoreCase))
            {
                var timeline = new ProgramTimelineRowDto
                {
                    Id = r.SourceRowId,
                    Title = title ?? "Untitled Timeline",
                    StartDate = start,
                    EndDate = end,
                    Status = status ?? "New",
                    Comments = comments,
                    Milestones = []
                };
                timelines.Add(timeline);
                timelineMap[r.SourceRowId] = timeline;
            }
            else // Milestone
            {
                r.DomainProperties.TryGetValue("OwnerEmails", out var ownerEmails);
                r.DomainProperties.TryGetValue("ParentTimelineSourceRowId", out var parentRowIdStr);

                var milestone = new ProgramMilestoneDto
                {
                    Id = r.SourceRowId,
                    Title = title ?? "Untitled Milestone",
                    StartDate = start,
                    EndDate = end,
                    OwnerEmails = ownerEmails ?? string.Empty,
                    Status = status ?? "New",
                    Comments = comments
                };

                if (!string.IsNullOrWhiteSpace(parentRowIdStr) &&
                    Guid.TryParse(parentRowIdStr, out var parentGuid) &&
                    timelineMap.TryGetValue(parentGuid, out var parentTimeline))
                {
                    milestone.ParentTimelineId = parentTimeline.Id;
                    parentTimeline.Milestones.Add(milestone);
                }
                else if (timelines.Count > 0)
                {
                    // Fallback to the latest timeline
                    var last = timelines[^1];
                    milestone.ParentTimelineId = last.Id;
                    last.Milestones.Add(milestone);
                }
            }
        }

        return timelines;
    }

    private static List<ProjectPhaseDto> BuildProjectPhases(IReadOnlyList<ProcessedDomainRowDto> rows)
    {
        var phases = new List<ProjectPhaseDto>();
        var phaseMap = new Dictionary<Guid, ProjectPhaseDto>();

        foreach (var r in rows)
        {
            r.DomainProperties.TryGetValue("EntryType", out var entryType);
            r.DomainProperties.TryGetValue("Title", out var title);
            r.DomainProperties.TryGetValue("StartDateUtc", out var startStr);
            r.DomainProperties.TryGetValue("EndDateUtc", out var endStr);
            r.DomainProperties.TryGetValue("Status", out var status);
            r.DomainProperties.TryGetValue("Comments", out var comments);

            DateTime? start = DateTime.TryParse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var s) ? s : null;
            DateTime? end = DateTime.TryParse(endStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var e) ? e : null;

            if (string.Equals(entryType, "Timeline", StringComparison.OrdinalIgnoreCase))
            {
                var phase = new ProjectPhaseDto
                {
                    Id = r.SourceRowId,
                    Title = title ?? "Untitled Phase",
                    StartDate = start,
                    EndDate = end,
                    Status = status ?? "New",
                    Comments = comments,
                    Milestones = []
                };
                phases.Add(phase);
                phaseMap[r.SourceRowId] = phase;
            }
            else // Milestone
            {
                r.DomainProperties.TryGetValue("OwnerEmails", out var ownerEmails);
                r.DomainProperties.TryGetValue("ParentTimelineSourceRowId", out var parentRowIdStr);

                var milestone = new ProjectMilestoneDto
                {
                    Id = r.SourceRowId,
                    Title = title ?? "Untitled Milestone",
                    StartDate = start,
                    EndDate = end,
                    OwnerEmails = ownerEmails ?? string.Empty,
                    Status = status ?? "New",
                    Comments = comments
                };

                if (!string.IsNullOrWhiteSpace(parentRowIdStr) &&
                    Guid.TryParse(parentRowIdStr, out var parentGuid) &&
                    phaseMap.TryGetValue(parentGuid, out var parentPhase))
                {
                    milestone.ParentTimelineId = parentPhase.Id;
                    parentPhase.Milestones.Add(milestone);
                }
                else if (phases.Count > 0)
                {
                    var last = phases[^1];
                    milestone.ParentTimelineId = last.Id;
                    last.Milestones.Add(milestone);
                }
            }
        }

        return phases;
    }
}
