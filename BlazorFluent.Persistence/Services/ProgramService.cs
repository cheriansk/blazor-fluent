using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Service implementation for managing enterprise programs within a tenant.
/// Enforces Zero Trust and role permissions.
/// </summary>
public class ProgramService : IProgramService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IHierarchyAuthorizationService _hierarchyAuth;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProgramService> _logger;

    public ProgramService(
        AppDbContext context,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IHierarchyAuthorizationService hierarchyAuth,
        IAuditService auditService,
        ILogger<ProgramService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _hierarchyAuth = hierarchyAuth;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProgramEntity>> GetProgramsAsync(string tenantSlug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug)) return [];

        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        var userId = _currentUser.UserId ?? string.Empty;

        // Zero Trust Gate: User must have access to tenant
        if (!await _hierarchyAuth.CanAccessTenantAsync(normalizedSlug, userId, ct))
        {
            _logger.LogWarning("Zero Trust Violation: User '{UserId}' denied access to query programs in tenant '{TenantSlug}'",
                userId, normalizedSlug);
            return [];
        }

        // Get list of accessible program IDs (Tenant Admin sees all, Program Admin sees only assigned)
        var accessibleProgramIds = await _hierarchyAuth.GetAccessibleProgramIdsAsync(normalizedSlug, userId, ct);

        return await _context.Programs
            .AsNoTracking()
            .Where(p => p.TenantId.ToLower() == normalizedSlug &&
                        accessibleProgramIds.Contains(p.Id) &&
                        !p.IsDeleted)
            .Include(p => p.Projects.Where(pr => !pr.IsDeleted))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Result<ProgramEntity>> GetProgramByIdAsync(Guid programId, CancellationToken ct = default)
    {
        if (programId == Guid.Empty) return Result<ProgramEntity>.Failure("Invalid Program ID.");

        var userId = _currentUser.UserId ?? string.Empty;

        if (!await _hierarchyAuth.CanAccessProgramAsync(programId, userId, ct))
        {
            return Result<ProgramEntity>.Failure("Access Denied: You do not have permissions to access this program.");
        }

        var program = await _context.Programs
            .AsNoTracking()
            .Include(p => p.Projects.Where(pr => !pr.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);

        if (program == null)
            return Result<ProgramEntity>.Failure("Program not found.");

        return Result<ProgramEntity>.Success(program);
    }

    public async Task<Result<ProgramEntity>> CreateProgramAsync(
        string tenantSlug,
        string name,
        string code,
        string description,
        DateTime? startDateUtc,
        DateTime? targetEndDateUtc,
        ProgramStatus status = ProgramStatus.Active,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug)) return Result<ProgramEntity>.Failure("Tenant slug is required.");
        if (string.IsNullOrWhiteSpace(name)) return Result<ProgramEntity>.Failure("Program name is required.");
        if (string.IsNullOrWhiteSpace(code)) return Result<ProgramEntity>.Failure("Program code is required.");

        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        var userId = _currentUser.UserId ?? string.Empty;

        // Zero Trust Gate: Creating programs requires Tenant Admin or Root Admin
        if (!await _hierarchyAuth.IsTenantAdminAsync(normalizedSlug, userId, ct))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to create a program in tenant '{TenantSlug}' without Tenant Admin privilege",
                userId, normalizedSlug);
            return Result<ProgramEntity>.Failure("Access Denied: Only Tenant Admins can create new programs.");
        }

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug.ToLower() == normalizedSlug, ct);

        if (tenant == null)
            return Result<ProgramEntity>.Failure($"Tenant '{tenantSlug}' was not found.");

        // Check unique code
        var codeExists = await _context.Programs
            .AnyAsync(p => p.TenantId.ToLower() == normalizedSlug &&
                           p.Code.ToLower() == code.Trim().ToLower() &&
                           !p.IsDeleted, ct);

        if (codeExists)
            return Result<ProgramEntity>.Failure($"A program with code '{code}' already exists in this tenant.");

        var program = new ProgramEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = normalizedSlug,
            TenantEntityId = tenant.Id,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim() ?? string.Empty,
            StartDateUtc = startDateUtc,
            TargetEndDateUtc = targetEndDateUtc,
            Status = status,
            IsActive = true
        };

        _context.Programs.Add(program);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogUserActivityAsync(
            $"Created program '{program.Name}' ({program.Code}) in tenant '{normalizedSlug}'",
            $"ProgramId: {program.Id}, Status: {program.Status}",
            ct);

        return Result<ProgramEntity>.Success(program);
    }

    public async Task<Result<ProgramEntity>> UpdateProgramAsync(
        Guid programId,
        string name,
        string code,
        string description,
        DateTime? startDateUtc,
        DateTime? targetEndDateUtc,
        ProgramStatus status,
        bool isActive,
        CancellationToken ct = default)
    {
        if (programId == Guid.Empty) return Result<ProgramEntity>.Failure("Invalid Program ID.");
        if (string.IsNullOrWhiteSpace(name)) return Result<ProgramEntity>.Failure("Program name is required.");
        if (string.IsNullOrWhiteSpace(code)) return Result<ProgramEntity>.Failure("Program code is required.");

        var userId = _currentUser.UserId ?? string.Empty;

        // Zero Trust Gate: Must be Program Admin or Tenant Admin
        if (!await _hierarchyAuth.IsProgramAdminAsync(programId, userId, ct))
        {
            return Result<ProgramEntity>.Failure("Access Denied: You do not have administrative permissions to edit this program.");
        }

        var program = await _context.Programs
            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);

        if (program == null)
            return Result<ProgramEntity>.Failure("Program not found.");

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (program.Code != normalizedCode)
        {
            var codeExists = await _context.Programs
                .AnyAsync(p => p.TenantId == program.TenantId &&
                               p.Code == normalizedCode &&
                               p.Id != programId &&
                               !p.IsDeleted, ct);

            if (codeExists)
                return Result<ProgramEntity>.Failure($"A program with code '{code}' already exists in this tenant.");
        }

        program.Name = name.Trim();
        program.Code = normalizedCode;
        program.Description = description?.Trim() ?? string.Empty;
        program.StartDateUtc = startDateUtc;
        program.TargetEndDateUtc = targetEndDateUtc;
        program.Status = status;
        program.IsActive = isActive;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogUserActivityAsync(
            $"Updated program '{program.Name}' ({program.Code})",
            $"ProgramId: {program.Id}, Status: {program.Status}, Active: {program.IsActive}",
            ct);

        return Result<ProgramEntity>.Success(program);
    }

    public async Task<IReadOnlyList<ProjectEntity>> GetProgramProjectsAsync(Guid programId, CancellationToken ct = default)
    {
        if (programId == Guid.Empty) return [];

        var userId = _currentUser.UserId ?? string.Empty;
        if (!await _hierarchyAuth.CanAccessProgramAsync(programId, userId, ct)) return [];

        return await _context.Projects
            .AsNoTracking()
            .Where(p => p.ProgramId == programId && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<ProgramHealthSummaryDto> GetProgramHealthAsync(Guid programId, CancellationToken ct = default)
    {
        if (programId == Guid.Empty) return new ProgramHealthSummaryDto();

        var program = await _context.Programs
            .AsNoTracking()
            .Include(p => p.Projects.Where(pr => !pr.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);

        if (program == null) return new ProgramHealthSummaryDto();

        var projectIds = program.Projects.Select(pr => pr.Id).ToList();

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => projectIds.Contains(t.ProjectId))
            .ToListAsync(ct);

        var dependencies = await _context.TaskDependencies
            .AsNoTracking()
            .Where(d => projectIds.Contains(d.ProjectId) || (d.DependsOnProjectId.HasValue && projectIds.Contains(d.DependsOnProjectId.Value)))
            .ToListAsync(ct);

        var projectLookup = program.Projects.ToDictionary(pr => pr.Id, pr => pr.Name);

        var totalTasks = tasks.Count;
        var completedTasks = tasks.Count(t => t.Status is UserTaskStatus.Closed or UserTaskStatus.Cancelled);
        var openTasks = tasks.Count(t => t.Status is UserTaskStatus.Open or UserTaskStatus.InProgress);
        var overdueTasks = tasks.Count(t => t.Status is not UserTaskStatus.Closed &&
                                            t.Status is not UserTaskStatus.Cancelled &&
                                            t.DueDate!=null &&
                                            t.DueDate.Date < DateTime.UtcNow.Date);

        // Identify Cross-Project Dependencies
        var crossProjectDeps = new List<CrossProjectDependencyDto>();
        var riskReasons = new List<string>();

        foreach (var dep in dependencies.Where(d => d.IsCrossProject))
        {
            var sourceTask = tasks.FirstOrDefault(t => t.Id == dep.TaskId);
            var targetTask = tasks.FirstOrDefault(t => t.Id == dep.DependsOnTaskId);

            var sourceProjectName = sourceTask != null && projectLookup.TryGetValue(sourceTask.ProjectId, out var sName) ? sName : "External Project";
            var targetProjectName = dep.DependsOnProjectId.HasValue && projectLookup.TryGetValue(dep.DependsOnProjectId.Value, out var tName) ? tName : "External Project";

            var isResolved = targetTask?.Status is UserTaskStatus.Closed or UserTaskStatus.Cancelled;
            var isOverdue = !isResolved && dep.ResolveByUtc.HasValue && dep.ResolveByUtc.Value.Date < DateTime.UtcNow.Date;

            crossProjectDeps.Add(new CrossProjectDependencyDto
            {
                DependencyId = dep.Id,
                SourceProjectId = dep.ProjectId,
                SourceProjectName = sourceProjectName,
                SourceTaskId = dep.TaskId,
                SourceTaskTitle = sourceTask?.Title ?? "Task",
                TargetProjectId = dep.DependsOnProjectId ?? Guid.Empty,
                TargetProjectName = targetProjectName,
                TargetTaskId = dep.DependsOnTaskId,
                TargetTaskTitle = targetTask?.Title ?? "Prerequisite Task",
                TargetAssigneeEmails = targetTask?.AssigneeEmails ?? string.Empty,
                DependencyType = dep.DependencyType,
                ResolveByUtc = dep.ResolveByUtc,
                IsResolved = isResolved
            });

            if (isOverdue && dep.DependencyType == TaskDependencyType.Blocks)
            {
                riskReasons.Add($"Cross-project blocker overdue: '{sourceTask?.Title}' in [{sourceProjectName}] is blocked by '{targetTask?.Title}' in [{targetProjectName}].");
            }
        }

        var activeCrossProjectBlockers = crossProjectDeps.Count(d => !d.IsResolved && d.DependencyType == TaskDependencyType.Blocks);

        var healthRating = ProgramHealthRating.OnTrack;
        if (riskReasons.Count > 0 || overdueTasks > 5)
        {
            healthRating = ProgramHealthRating.Critical;
        }
        else if (activeCrossProjectBlockers > 0 || overdueTasks > 0)
        {
            healthRating = ProgramHealthRating.AtRisk;
            if (activeCrossProjectBlockers > 0)
                riskReasons.Add($"{activeCrossProjectBlockers} active cross-project blocking dependency(ies) currently unresolved.");
        }

        return new ProgramHealthSummaryDto
        {
            ProgramId = program.Id,
            ProgramName = program.Name,
            ProgramCode = program.Code,
            Status = program.Status,
            Health = healthRating,
            TotalProjects = program.Projects.Count,
            ActiveProjects = program.Projects.Count(pr => pr.IsActive),
            ProjectsAtRisk = healthRating == ProgramHealthRating.Critical ? 1 : 0,
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            OpenTasks = openTasks,
            OverdueTasks = overdueTasks,
            CrossProjectBlockersCount = activeCrossProjectBlockers,
            CrossProjectDependencies = crossProjectDeps,
            RiskReasons = riskReasons
        };
    }
}
