using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Implements project authorization and role management with direct real-time database queries
/// and forensic security audit logging on unauthorized attempts.
/// </summary>
public class ProjectAuthorizationService : IProjectAuthorizationService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProjectAuthorizationService> _logger;

    public ProjectAuthorizationService(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IAuditService auditService,
        ILogger<ProjectAuthorizationService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ProjectRole?> GetCurrentUserProjectRoleAsync(Guid projectId, CancellationToken ct = default)
    {
        // Root Admin bypass
        if (_currentUser.IsRootAdmin)
        {
            return ProjectRole.Admin;
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var userId = _currentUser.UserId ?? "anonymous";
        var roleEntity = await _dbContext.ProjectUserRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ProjectId == projectId && r.UserId == userId && !r.IsDeleted, ct);

        return roleEntity?.Role;
    }

    public async Task<bool> CanVisitAsync(Guid projectId, ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default)
    {
        var role = await GetCurrentUserProjectRoleAsync(projectId, ct);
        return role.HasValue && role.Value.Satisfies(minRole);
    }

    public async Task<bool> CanEditAsync(Guid projectId, ProjectRole minRole = ProjectRole.Dev, CancellationToken ct = default)
    {
        var role = await GetCurrentUserProjectRoleAsync(projectId, ct);
        return role.HasValue && role.Value.Satisfies(minRole) && role.Value.CanWrite();
    }

    public async Task EnsureCanVisitAsync(Guid projectId, ProjectRole minRole = ProjectRole.ReadOnly, string operation = "", CancellationToken ct = default)
    {
        var canVisit = await CanVisitAsync(projectId, minRole, ct);
        if (canVisit) return;

        var userId = _currentUser.UserId ?? "anonymous";
        _logger.LogWarning("Security Violation: User '{UserId}' attempted unauthorized read on project '{ProjectId}'. Required role: '{MinRole}'. Operation: '{Operation}'",
            userId, projectId, minRole, operation);

        await _auditService.LogSecurityEventAsync(
            "ProjectVisitDenied",
            AuditSeverity.Warning,
            $"Access Denied: User '{userId}' attempted unauthorized visit on project '{projectId}' for operation '{operation}'. Required role: '{minRole}'.",
            ct);

        throw new UnauthorizedAccessException($"Access Denied: You do not have permission to view project '{projectId}'. Required minimum role: '{minRole}'.");
    }

    public async Task EnsureCanEditAsync(Guid projectId, ProjectRole minRole = ProjectRole.Dev, string operation = "", CancellationToken ct = default)
    {
        var canEdit = await CanEditAsync(projectId, minRole, ct);
        if (canEdit) return;

        var userId = _currentUser.UserId ?? "anonymous";
        _logger.LogWarning("Security Violation: User '{UserId}' attempted unauthorized write on project '{ProjectId}'. Required role: '{MinRole}'. Operation: '{Operation}'",
            userId, projectId, minRole, operation);

        await _auditService.LogSecurityEventAsync(
            "ProjectMutationDenied",
            AuditSeverity.Critical,
            $"Access Denied: User '{userId}' attempted unauthorized mutation on project '{projectId}' for operation '{operation}'. Required role: '{minRole}'.",
            ct);

        throw new UnauthorizedAccessException($"Access Denied: You do not have permission to modify project '{projectId}'. Required minimum role: '{minRole}'.");
    }

    public async Task<IReadOnlyList<Guid>> GetAuthorizedProjectIdsAsync(ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        if (_currentUser.IsRootAdmin)
        {
            return await _dbContext.Projects
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        var userId = _currentUser.UserId ?? "anonymous";
        var userRoles = await _dbContext.ProjectUserRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.UserId == userId && !r.IsDeleted)
            .ToListAsync(ct);

        return userRoles
            .Where(r => r.Role.Satisfies(minRole))
            .Select(r => r.ProjectId)
            .Distinct()
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectUserRoleEntity>> GetProjectMembersAsync(Guid projectId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        return await _dbContext.ProjectUserRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ProjectId == projectId && !r.IsDeleted)
            .OrderBy(r => r.UserName)
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
        // Only ProjectAdmin, TenantAdmin, or Root Admin can manage access
        var callerRole = await GetCurrentUserProjectRoleAsync(projectId, ct);
        if (!_currentUser.IsRootAdmin && (callerRole is null || !callerRole.Value.CanManageAccess()))
        {
            return Result.Failure("Only Project Admins or Tenant Admins can assign or change project roles.");
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var existing = await _dbContext.ProjectUserRoles
            .AsTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ProjectId == projectId && r.UserId == userId && !r.IsDeleted, ct);

        if (existing is not null)
        {
            existing.Role = role;
            existing.UserEmail = userEmail;
            existing.UserName = userName;
            existing.UserType = userType;
        }
        else
        {
            var assignment = new ProjectUserRoleEntity
            {
                TenantId = tenantId,
                ProjectId = projectId,
                UserId = userId,
                UserEmail = userEmail,
                UserName = userName,
                UserType = userType,
                Role = role
            };
            _dbContext.ProjectUserRoles.Add(assignment);
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogUserActivityAsync(
            "AssignProjectRole",
            $"Role '{role}' assigned to user '{userName}' ({userId}) on project '{projectId}'.",
            ct);

        return Result.Success();
    }

    public async Task<Result> RevokeUserRoleAsync(Guid projectId, string userId, CancellationToken ct = default)
    {
        var callerRole = await GetCurrentUserProjectRoleAsync(projectId, ct);
        if (!_currentUser.IsRootAdmin && (callerRole is null || !callerRole.Value.CanManageAccess()))
        {
            return Result.Failure("Only Project Admins or Tenant Admins can revoke project roles.");
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var existing = await _dbContext.ProjectUserRoles
            .AsTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ProjectId == projectId && r.UserId == userId && !r.IsDeleted, ct);

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
