using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Implements project authorization and role management with direct real-time database queries,
/// strict tenant roster gating, and forensic security audit logging on unauthorized attempts.
/// </summary>
public class ProjectAuthorizationService : IProjectAuthorizationService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IHierarchyAuthorizationService _hierarchyAuth;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProjectAuthorizationService> _logger;

    public ProjectAuthorizationService(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IHierarchyAuthorizationService hierarchyAuth,
        IAuditService auditService,
        ILogger<ProjectAuthorizationService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _hierarchyAuth = hierarchyAuth;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ProjectRole?> GetCurrentUserProjectRoleAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _hierarchyAuth.GetEffectiveProjectRoleAsync(projectId, _currentUser.UserId, ct);
    }

    public async Task<bool> CanVisitAsync(Guid projectId, ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default)
    {
        return await _hierarchyAuth.HasProjectClearanceAsync(projectId, minRole, _currentUser.UserId, ct);
    }

    public async Task<bool> CanEditAsync(Guid projectId, ProjectRole minRole = ProjectRole.Dev, CancellationToken ct = default)
    {
        return await _hierarchyAuth.HasProjectClearanceAsync(projectId, minRole, _currentUser.UserId, ct);
    }

    public async Task EnsureCanVisitAsync(Guid projectId, ProjectRole minRole = ProjectRole.ReadOnly, string operation = "", CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProjectClearanceAsync(projectId, minRole, _currentUser.UserId, ct);
    }

    public async Task EnsureCanEditAsync(Guid projectId, ProjectRole minRole = ProjectRole.Dev, string operation = "", CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProjectClearanceAsync(projectId, minRole, _currentUser.UserId, ct);
    }

    public async Task<bool> HasClearanceAsync(Guid projectId, ProjectRole minimumRole, CancellationToken ct = default)
    {
        return await _hierarchyAuth.HasProjectClearanceAsync(projectId, minimumRole, _currentUser.UserId, ct);
    }

    public async Task EnsureCanContributeAsync(Guid projectId, string? operation = null, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProjectClearanceAsync(projectId, ProjectRole.QA, _currentUser.UserId, ct);
    }

    public async Task EnsureCanManageWorkflowAsync(Guid projectId, string? operation = null, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProjectClearanceAsync(projectId, ProjectRole.Admin, _currentUser.UserId, ct);
    }

    public async Task EnsureCanManageAccessAsync(Guid projectId, string? operation = null, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureHasProjectClearanceAsync(projectId, ProjectRole.Admin, _currentUser.UserId, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAuthorizedProjectIdsAsync(ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var userId = _currentUser.UserId ?? "anonymous";
        var hasGuid = Guid.TryParse(userId, out var userGuid);
        var normalizedUser = userId.Trim().ToLowerInvariant();

        // If Tenant Admin, all projects in the tenant meet any clearance requirement
        if (await _hierarchyAuth.IsTenantAdminAsync(tenantId, userId, ct))
        {
            return await _dbContext.Projects
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        var userRoles = await _dbContext.ProjectUserRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId &&
                        ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&
                        !r.IsDeleted)
            .ToListAsync(ct);

        return userRoles
            .Where(r => r.Role.Satisfies(minRole))
            .Select(r => r.ProjectId)
            .Distinct()
            .ToList();
    }

    public Task<IReadOnlyList<Guid>> GetAccessibleProjectIdsAsync(ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default)
        => GetAuthorizedProjectIdsAsync(minRole, ct);

    public async Task<IReadOnlyList<ProjectUserRoleEntity>> GetProjectMembersAsync(Guid projectId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        return await _dbContext.ProjectUserRoles
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.TenantId == tenantId && r.ProjectId == projectId && !r.IsDeleted)
            .OrderBy(r => r.User!.FullName)
            .ToListAsync(ct);
    }

    public async Task<Result> AssignUserRoleAsync(
        Guid projectId,
        string userId,
        string userEmail,
        string userName,
        UserType userType,
        ProjectRole role,
        CancellationToken ct = default)
    {
        // Separation of duties: Appointing Project Admin requires CanManageProjectAdminsAsync
        if (role == ProjectRole.Admin)
        {
            var canManageAdmins = await _hierarchyAuth.CanManageProjectAdminsAsync(projectId, _currentUser.UserId, ct);
            if (!canManageAdmins)
            {
                return Result.Failure("Only Project Admins, parent Program Admins, or Tenant Admins can assign or change Project Admin privileges.");
            }
        }
        else
        {
            var callerRole = await GetCurrentUserProjectRoleAsync(projectId, ct);
            if (callerRole is null || !callerRole.Value.CanManageAccess())
            {
                return Result.Failure("Only Project Admins, parent Program Admins, or Tenant Admins can assign or change project roles.");
            }
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        if (!Guid.TryParse(userId, out var userEntityId))
        {
            var resolvedUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.Trim().ToLower() && !u.IsDeleted, ct);

            if (resolvedUser is null)
            {
                return Result.Failure($"User '{userEmail}' could not be resolved in the user directory.");
            }

            userEntityId = resolvedUser.Id;
        }

        // Strict Tenant Roster Gate: Target user must be an active member of this tenant's roster
        var inTenantRoster = await _dbContext.TenantUsers
            .AsNoTracking()
            .AnyAsync(tu => tu.TenantId == tenantId && tu.UserEntityId == userEntityId && tu.IsActive && !tu.IsDeleted, ct);

        if (!inTenantRoster)
        {
            return Result.Failure($"Zero-Trust Boundary Violation: User '{userEmail}' is not an active member of tenant '{tenantId}'. Tenant Admins must first add the user to the tenant roster.");
        }

        var existing = await _dbContext.ProjectUserRoles
            .AsTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ProjectId == projectId && r.UserEntityId == userEntityId && !r.IsDeleted, ct);

        if (existing is not null)
        {
            existing.Role = role;
        }
        else
        {
            var assignment = new ProjectUserRoleEntity
            {
                TenantId = tenantId,
                ProjectId = projectId,
                UserEntityId = userEntityId,
                Role = role
            };
            _dbContext.ProjectUserRoles.Add(assignment);
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogUserActivityAsync(
            "AssignProjectRole",
            $"Role '{role}' assigned to user '{userName}' ({userEntityId}) on project '{projectId}'.",
            ct);

        return Result.Success();
    }

    public async Task<Result> RevokeUserRoleAsync(Guid projectId, string userId, CancellationToken ct = default)
    {
        var callerRole = await GetCurrentUserProjectRoleAsync(projectId, ct);
        if (callerRole is null || !callerRole.Value.CanManageAccess())
        {
            return Result.Failure("Only Project Admins, parent Program Admins, or Tenant Admins can revoke project roles.");
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var hasGuid = Guid.TryParse(userId, out var userGuid);
        var normalizedUser = userId.Trim().ToLowerInvariant();

        var existing = await _dbContext.ProjectUserRoles
            .AsTracking()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId &&
                                      r.ProjectId == projectId &&
                                      ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&
                                      !r.IsDeleted, ct);

        if (existing is null)
        {
            return Result.Failure("No active role assignment found for the specified user and project.");
        }

        existing.IsDeleted = true;
        existing.DeletedAtUtc = DateTime.UtcNow;
        existing.DeletedBy = _currentUser.UserId;

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogUserActivityAsync(
            "RevokeProjectRole",
            $"Role revoked for user '{existing.UserName}' ({userId}) on project '{projectId}'.",
            ct);

        return Result.Success();
    }
}
