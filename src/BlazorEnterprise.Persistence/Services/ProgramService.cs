using BlazorEnterprise.Persistence.Extensions;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Service implementation for managing enterprise programs within a tenant.
/// Enforces Zero Trust and role permissions.
/// </summary>
public class ProgramService : IProgramService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IHierarchyAuthorizationService _hierarchyAuth;
    private readonly IAuditService _auditService;
    private readonly ISafeSaveService _safeSaveService;
    private readonly ILogger<ProgramService> _logger;

    public ProgramService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IHierarchyAuthorizationService hierarchyAuth,
        IAuditService auditService,
        ISafeSaveService safeSaveService,
        ILogger<ProgramService> logger)
    {
        _dbFactory = dbFactory;
        _safeSaveService = safeSaveService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _hierarchyAuth = hierarchyAuth;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProgramEntity>> GetProgramsAsync(string tenantSlug, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        if (string.IsNullOrWhiteSpace(tenantSlug)) return [];

        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        var userId = _currentUser.UserId;

        // Zero Trust Gate: User must have access to tenant
        if (!await _hierarchyAuth.CanAccessTenantAsync(normalizedSlug, userId, ct))
        {
            _logger.LogWarning("Zero Trust Violation: User '{UserId}' denied access to query programs in tenant '{TenantSlug}'",
                userId, normalizedSlug);
            return [];
        }

        // Get list of accessible program IDs (Tenant Admin sees all, Program Admin sees only assigned)
        var accessibleProgramIds = await _hierarchyAuth.GetAccessibleProgramIdsAsync(normalizedSlug, userId, ct);

        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == normalizedSlug && !t.IsDeleted, ct);
        if (tenant == null) return [];

        return await _context.Programs
            .AsNoTracking()
            .Where(p => p.TenantId == tenant.Id &&
                        accessibleProgramIds.Contains(p.Id) &&
                        !p.IsDeleted)
            .Include(p => p.Projects.Where(pr => !pr.IsDeleted))
                .ThenInclude(pr => pr.UserRoles.Where(ur => !ur.IsDeleted))
                    .ThenInclude(ur => ur.User)
            .Include(p => p.UserRoles.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.User)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Result<ProgramEntity>> GetProgramByIdAsync(Guid programId, bool forEdit = false, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        if (programId == Guid.Empty) return Result<ProgramEntity>.Failure("Invalid Program ID.");

        var userId = _currentUser.UserId;

        if (!await _hierarchyAuth.CanAccessProgramAsync(programId, userId, ct))
        {
            return Result<ProgramEntity>.Failure("Access Denied: You do not have permissions to access this program.");
        }

        var program = await _context.Programs
            .WithTracking(forEdit)
            .Include(p => p.Projects.Where(pr => !pr.IsDeleted))
                .ThenInclude(pr => pr.UserRoles.Where(ur => !ur.IsDeleted))
                    .ThenInclude(ur => ur.User)
            .Include(p => p.UserRoles.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.User)
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
        ProgramStatusEnum status = ProgramStatusEnum.Active,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        if (string.IsNullOrWhiteSpace(tenantSlug)) return Result<ProgramEntity>.Failure("Tenant slug is required.");
        if (string.IsNullOrWhiteSpace(name)) return Result<ProgramEntity>.Failure("Program name is required.");
        if (string.IsNullOrWhiteSpace(code)) return Result<ProgramEntity>.Failure("Program code is required.");

        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        var userId = _currentUser.UserId;

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
            .AnyAsync(p => p.TenantId == tenant.Id &&
                           p.Code.ToLower() == code.Trim().ToLower() &&
                           !p.IsDeleted, ct);

        if (codeExists)
            return Result<ProgramEntity>.Failure($"A program with code '{code}' already exists in this tenant.");

        var program = new ProgramEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            StartDate = startDateUtc ?? DateTime.UtcNow,
            DueDate = targetEndDateUtc ?? (startDateUtc ?? DateTime.UtcNow).AddMonths(3),
            EndDate = targetEndDateUtc,
            Status = status,
            IsActive = true
        };

        _context.Programs.Add(program);
        _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

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
        ProgramStatusEnum status,
        bool isActive,
        uint version = 0,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        if (programId == Guid.Empty) return Result<ProgramEntity>.Failure("Invalid Program ID.");
        if (string.IsNullOrWhiteSpace(name)) return Result<ProgramEntity>.Failure("Program name is required.");
        if (string.IsNullOrWhiteSpace(code)) return Result<ProgramEntity>.Failure("Program code is required.");

        var userId = _currentUser.UserId;

        // Zero Trust Gate: Must be Program Admin or Tenant Admin
        if (!await _hierarchyAuth.IsProgramAdminAsync(programId, userId, ct))
        {
            return Result<ProgramEntity>.Failure("Access Denied: You do not have administrative permissions to edit this program.");
        }

        var program = await _context.Programs
            .AsTracking()
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

        if (version > 0)
        {
            _context.ApplyOriginalVersion(program, new VersionHolder(version));
        }

        var saveResult = await _safeSaveService.SaveAsync(_context, ct);
        if (saveResult.Status == SaveStatus.Conflict)
        {
            return Result<ProgramEntity>.Failure("Concurrency Conflict: Another user updated this program while you were editing. To prevent data loss, no changes were saved. Please re-open the program to view the latest changes.");
        }
        if (saveResult.Status != SaveStatus.Ok)
        {
            return Result<ProgramEntity>.Failure(saveResult.Message ?? "Failed to update program due to database constraint violation.");
        }

        await _auditService.LogUserActivityAsync(
            $"Updated program '{program.Name}' ({program.Code})",
            $"ProgramId: {program.Id}, Status: {program.Status}, Active: {program.IsActive}",
            ct);

        return Result<ProgramEntity>.Success(program);
    }

    public async Task<Result> SyncProgramAdminsAsync(Guid programId, IEnumerable<Guid> adminUserIds, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        if (programId == Guid.Empty) return Result.Failure("Invalid Program ID.");

        var program = await _context.Programs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);
        if (program == null) return Result.Failure("Program not found.");

        var targetAdminIds = (adminUserIds ?? Enumerable.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();

        var existingRoles = await _context.ProgramUserRoles
            .AsTracking()
            .Where(r => r.ProgramId == programId)
            .ToListAsync(ct);

        // Soft-delete removed admins
        foreach (var role in existingRoles)
        {
            if (role.Role == ProgramRoleEnum.Admin)
            {
                if (!targetAdminIds.Contains(role.UserEntityId))
                {
                    if (!role.IsDeleted)
                    {
                        role.IsDeleted = true;
                        role.DeletedAtUtc = DateTime.UtcNow;
                        role.DeletedBy = _currentUser.UserId;
                    }
                }
                else
                {
                    // Restore if previously deleted
                    if (role.IsDeleted)
                    {
                        role.IsDeleted = false;
                        role.DeletedAtUtc = null;
                        role.DeletedBy = null;
                    }
                }
            }
        }

        // Add new admins
        var existingUserIds = existingRoles.Where(r => r.Role == ProgramRoleEnum.Admin).Select(r => r.UserEntityId).ToHashSet();
        foreach (var adminId in targetAdminIds)
        {
            if (!existingUserIds.Contains(adminId))
            {
                _context.ProgramUserRoles.Add(new ProgramUserRoleEntity
                {
                    TenantId = program.TenantId,
                    ProgramId = programId,
                    UserEntityId = adminId,
                    Role = ProgramRoleEnum.Admin
                });
            }
        }

        _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
        return Result.Success();
    }

    public async Task<IReadOnlyList<ProjectEntity>> GetProgramProjectsAsync(Guid programId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        if (programId == Guid.Empty) return [];

        var userId = _currentUser.UserId;
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
        await _hierarchyAuth.EnsureHasProgramClearanceAsync(programId, ProgramRoleEnum.ReadOnly, ct: ct);
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);

        var program = await _context.Programs
            .AsNoTracking()
            .Include(p => p.Projects.Where(pr => !pr.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);

        if (program == null) return new ProgramHealthSummaryDto();

        var projectIds = program.Projects.Select(pr => pr.Id).ToList();

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId.HasValue && projectIds.Contains(t.ProjectId.Value))
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();
        var taskProjectMap = tasks.Where(t => t.ProjectId.HasValue).ToDictionary(t => t.Id, t => t.ProjectId!.Value);

        var dependencies = await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => !d.IsDeleted &&
                        ((d.ProjectId.HasValue && projectIds.Contains(d.ProjectId.Value)) ||
                         (d.DependsOnItemId.HasValue && taskIds.Contains(d.DependsOnItemId.Value))))
            .ToListAsync(ct);

        // Resolve unmapped DependsOnProjectId in memory from known tasks
        foreach (var dep in dependencies)
        {
            if (dep.DependsOnItemId.HasValue && taskProjectMap.TryGetValue(dep.DependsOnItemId.Value, out var dependsOnPid))
            {
                dep.DependsOnProjectId = dependsOnPid;
            }
        }

        var projectLookup = program.Projects.ToDictionary(pr => pr.Id, pr => pr.Name);

        var totalTasks = tasks.Count;
        var completedTasks = tasks.Count(t => t.Status is TaskStatusEnum.Closed or TaskStatusEnum.Cancelled);
        var openTasks = tasks.Count(t => t.Status is TaskStatusEnum.Open or TaskStatusEnum.InProgress);
        var overdueTasks = tasks.Count(t => t.Status is not TaskStatusEnum.Closed &&
                                            t.Status is not TaskStatusEnum.Cancelled &&
                                            t.DueDate.Date < DateTime.UtcNow.Date);

        // Identify Cross-Project Dependencies
        var crossProjectDeps = new List<CrossProjectDependencyDto>();
        var riskReasons = new List<string>();

        foreach (var dep in dependencies.Where(d => d.DependsOnProjectId.HasValue && d.ProjectId != d.DependsOnProjectId.Value))
        {
            var sourceTask = tasks.FirstOrDefault(t => t.Id == dep.DependentItemId);
            var targetTask = tasks.FirstOrDefault(t => t.Id == dep.DependsOnItemId);

            string sourceProjectName = "External Project";
            if (sourceTask?.ProjectId != null && projectLookup.TryGetValue(sourceTask.ProjectId.Value, out var sName))
            {
                sourceProjectName = sName;
            }
            else if (dep.ProjectId.HasValue && projectLookup.TryGetValue(dep.ProjectId.Value, out var sProj))
            {
                sourceProjectName = sProj;
            }

            string targetProjectName = "External Project";
            if (dep.DependsOnProjectId.HasValue && projectLookup.TryGetValue(dep.DependsOnProjectId.Value, out var tName))
            {
                targetProjectName = tName;
            }

            var isResolved = dep.IsResolved || (targetTask?.Status is TaskStatusEnum.Closed or TaskStatusEnum.Cancelled);
            var isOverdue = !isResolved && dep.DueDate.Date < DateTime.UtcNow.Date;

            var targetProjectId = dep.DependsOnProjectId ?? Guid.Empty;
            var canAccessTarget = targetProjectId != Guid.Empty && await _hierarchyAuth.CanViewScopedItemAsync(TaskLevelEnum.Project, program.TenantId, program.Id, targetProjectId, ct: ct);

            crossProjectDeps.Add(new CrossProjectDependencyDto
            {
                DependencyId = dep.Id,
                SourceProjectId = dep.ProjectId ?? Guid.Empty,
                SourceProjectName = sourceProjectName,
                SourceTaskId = dep.DependentItemId ?? Guid.Empty,
                SourceTaskTitle = sourceTask?.Title ?? dep.Title,
                TargetProjectId = targetProjectId,
                TargetProjectName = targetProjectName,
                TargetTaskId = dep.DependsOnItemId ?? Guid.Empty,
                TargetTaskTitle = targetTask?.Title ?? dep.DependsOnSnapshotTitle ?? "Prerequisite Item",
                TargetAssigneeEmails = targetTask?.AssigneeEmails ?? string.Empty,
                DependencyType = dep.DependencyType,
                ResolveByUtc = dep.DueDate,
                IsResolved = isResolved,
                CanAccessTargetItem = canAccessTarget
            });

            if (isOverdue && dep.DependencyType == TaskDependencyTypeEnum.Blocks)
            {
                riskReasons.Add($"Cross-project blocker overdue: '{sourceTask?.Title ?? dep.Title}' in [{sourceProjectName}] is blocked by '{targetTask?.Title ?? dep.DependsOnSnapshotTitle ?? "Prerequisite"}' in [{targetProjectName}].");
            }
        }

        var activeCrossProjectBlockers = crossProjectDeps.Count(d => !d.IsResolved && d.DependencyType == TaskDependencyTypeEnum.Blocks);

        var healthRating = ProgramHealthRatingEnum.OnTrack;
        if (riskReasons.Count > 0 || overdueTasks > 5)
        {
            healthRating = ProgramHealthRatingEnum.Critical;
        }
        else if (activeCrossProjectBlockers > 0 || overdueTasks > 0)
        {
            healthRating = ProgramHealthRatingEnum.AtRisk;
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
            ProjectsAtRisk = healthRating == ProgramHealthRatingEnum.Critical ? 1 : 0,
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            OpenTasks = openTasks,
            OverdueTasks = overdueTasks,
            CrossProjectBlockersCount = activeCrossProjectBlockers,
            CrossProjectDependencies = crossProjectDeps,
            RiskReasons = riskReasons
        };
    }

    public async Task<IReadOnlyList<RiskEntity>> GetProgramRisksAsync(Guid programId, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProgramClearanceAsync(programId, ProgramRoleEnum.ReadOnly, ct: ct);
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        return await _context.Risks
            .AsNoTracking()
            .Where(r => r.ProgramId == programId && !r.IsDeleted)
            .OrderByDescending(r => r.Created)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AssumptionEntity>> GetProgramAssumptionsAsync(Guid programId, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProgramClearanceAsync(programId, ProgramRoleEnum.ReadOnly, ct: ct);
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        return await _context.Assumptions
            .AsNoTracking()
            .Where(a => a.ProgramId == programId && !a.IsDeleted)
            .OrderByDescending(a => a.Created)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IssueEntity>> GetProgramIssuesAsync(Guid programId, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProgramClearanceAsync(programId, ProgramRoleEnum.ReadOnly, ct: ct);
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        return await _context.Issues
            .AsNoTracking()
            .Where(i => i.ProgramId == programId && !i.IsDeleted)
            .OrderByDescending(i => i.Created)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkDependencyEntity>> GetProgramBlockersAsync(Guid programId, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProgramClearanceAsync(programId, ProgramRoleEnum.ReadOnly, ct: ct);
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        return await _context.WorkDependencies
            .AsNoTracking()
            .Where(b => b.ProgramId == programId && !b.IsDeleted)
            .OrderByDescending(b => b.Created)
            .ToListAsync(ct);
    }
}
