using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Authoritative Zero Trust implementation of 3-tier hierarchical security:
/// Tenant Admin -> Program Admin -> Project Admin.
/// Queries PostgreSQL directly with live AsNoTracking execution.
/// </summary>
public class HierarchyAuthorizationService : IHierarchyAuthorizationService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<HierarchyAuthorizationService> _logger;

    public HierarchyAuthorizationService(
        AppDbContext context,
        ICurrentUser currentUser,
        ILogger<HierarchyAuthorizationService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<bool> IsTenantAdminAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId)) return false;

        var normalizedTenant = tenantId.Trim().ToLowerInvariant();
        var normalizedUser = userId.Trim().ToLowerInvariant();

        // 1. Check explicit TenantUser membership
        var hasTenantAdminRole = await _context.TenantUsers
            .AsNoTracking()
            .AnyAsync(u => u.TenantId.ToLower() == normalizedTenant &&
                           u.UserId.ToLower() == normalizedUser &&
                           u.Role == TenantRole.TenantAdmin &&
                           u.IsActive &&
                           !u.IsDeleted, ct);

        if (hasTenantAdminRole) return true;

        // 2. Compatibility check: User is internal CompanyUser with home tenant matching tenantId and no explicit member record yet
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => (u.Email.ToLower() == normalizedUser || u.Id.ToString() == normalizedUser) && !u.IsDeleted, ct);

        if (user != null && user.UserType == UserType.CompanyUser &&
            string.Equals(user.DefaultTenantId, normalizedTenant, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public async Task<bool> IsProgramAdminAsync(Guid programId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        if (programId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;

        var program = await _context.Programs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);

        if (program == null) return false;

        // Parent Tenant Admin has full administrative rights over all child programs
        if (await IsTenantAdminAsync(program.TenantId, userId, ct)) return true;

        var normalizedUser = userId.Trim().ToLowerInvariant();

        return await _context.ProgramUserRoles
            .AsNoTracking()
            .AnyAsync(r => r.ProgramId == programId &&
                           r.UserId.ToLower() == normalizedUser &&
                           r.Role == ProgramRole.ProgramAdmin &&
                           !r.IsDeleted, ct);
    }

    public async Task<bool> IsProjectAdminAsync(Guid projectId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;

        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);

        if (project == null) return false;

        // 1. Tenant Admin has full access
        if (await IsTenantAdminAsync(project.TenantId, userId, ct)) return true;

        // 2. Parent Program Admin has full access
        if (project.ProgramId.HasValue && await IsProgramAdminAsync(project.ProgramId.Value, userId, ct)) return true;

        // 3. Project Admin role assignment
        var normalizedUser = userId.Trim().ToLowerInvariant();

        return await _context.ProjectUserRoles
            .AsNoTracking()
            .AnyAsync(r => r.ProjectId == projectId &&
                           (r.UserId.ToLower() == normalizedUser || r.UserEmail.ToLower() == normalizedUser) &&
                           r.Role == ProjectRole.Admin &&
                           !r.IsDeleted, ct);
    }

    public async Task<bool> CanViewTenantSettingsAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId)) return false;

        // Tenant Admin can view
        if (await IsTenantAdminAsync(tenantId, userId, ct)) return true;

        var normalizedTenant = tenantId.Trim().ToLowerInvariant();
        var normalizedUser = userId.Trim().ToLowerInvariant();

        // Program Admin of ANY program in this tenant can view tenant settings in read-only mode
        return await _context.ProgramUserRoles
            .AsNoTracking()
            .AnyAsync(r => r.TenantId.ToLower() == normalizedTenant &&
                           r.UserId.ToLower() == normalizedUser &&
                           r.Role == ProgramRole.ProgramAdmin &&
                           !r.IsDeleted, ct);
    }

    public async Task<bool> CanEditTenantSettingsAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        // Strictly Tenant Admin can edit tenant settings; Program Admins are read-only
        return await IsTenantAdminAsync(tenantId, userId, ct);
    }

    public async Task<bool> CanAccessTenantAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId)) return false;

        var normalizedTenant = tenantId.Trim().ToLowerInvariant();
        var normalizedUser = userId.Trim().ToLowerInvariant();

        // Check TenantUser membership
        var hasMembership = await _context.TenantUsers
            .AsNoTracking()
            .AnyAsync(u => u.TenantId.ToLower() == normalizedTenant &&
                           u.UserId.ToLower() == normalizedUser &&
                           u.IsActive &&
                           !u.IsDeleted, ct);

        if (hasMembership) return true;

        // Check fallback home tenant
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => (u.Email.ToLower() == normalizedUser || u.Id.ToString() == normalizedUser) && !u.IsDeleted, ct);

        if (user != null && string.Equals(user.DefaultTenantId, normalizedTenant, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if user has any project assignment in this tenant
        return await _context.ProjectUserRoles
            .AsNoTracking()
            .AnyAsync(r => r.TenantId.ToLower() == normalizedTenant &&
                           (r.UserId.ToLower() == normalizedUser || r.UserEmail.ToLower() == normalizedUser) &&
                           !r.IsDeleted, ct);
    }

    public async Task<bool> CanAccessProgramAsync(Guid programId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        if (programId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;

        var program = await _context.Programs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);

        if (program == null) return false;

        // Tenant Admin can access all programs in the tenant
        if (await IsTenantAdminAsync(program.TenantId, userId, ct)) return true;

        var normalizedUser = userId.Trim().ToLowerInvariant();

        // Explicit Program Role
        var hasProgramRole = await _context.ProgramUserRoles
            .AsNoTracking()
            .AnyAsync(r => r.ProgramId == programId &&
                           r.UserId.ToLower() == normalizedUser &&
                           !r.IsDeleted, ct);

        if (hasProgramRole) return true;

        // Member of any project under this program
        var projectIdsInProgram = await _context.Projects
            .AsNoTracking()
            .Where(p => p.ProgramId == programId && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(ct);

        return await _context.ProjectUserRoles
            .AsNoTracking()
            .AnyAsync(r => projectIdsInProgram.Contains(r.ProjectId) &&
                           (r.UserId.ToLower() == normalizedUser || r.UserEmail.ToLower() == normalizedUser) &&
                           !r.IsDeleted, ct);
    }

    public async Task<bool> CanAccessProjectAsync(Guid projectId, string userId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;

        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);

        if (project == null) return false;

        // Tenant Admin has access
        if (await IsTenantAdminAsync(project.TenantId, userId, ct)) return true;

        // Parent Program Admin has access
        if (project.ProgramId.HasValue && await IsProgramAdminAsync(project.ProgramId.Value, userId, ct)) return true;

        // Explicit Project Role assignment
        var normalizedUser = userId.Trim().ToLowerInvariant();

        return await _context.ProjectUserRoles
            .AsNoTracking()
            .AnyAsync(r => r.ProjectId == projectId &&
                           (r.UserId.ToLower() == normalizedUser || r.UserEmail.ToLower() == normalizedUser) &&
                           !r.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleProgramIdsAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return [];

        var normalizedTenant = tenantId.Trim().ToLowerInvariant();

        // Root Admin and Tenant Admin have full visibility to all programs in the tenant
        if (_currentUser.IsRootAdmin || await IsTenantAdminAsync(tenantId, userId, ct))
        {
            return await _context.Programs
                .AsNoTracking()
                .Where(p => p.TenantId.ToLower() == normalizedTenant && !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        var normalizedUser = userId.Trim().ToLowerInvariant();

        // Program Admin / Member direct assignments
        var directlyAssignedProgramIds = await _context.ProgramUserRoles
            .AsNoTracking()
            .Where(r => r.TenantId.ToLower() == normalizedTenant &&
                        r.UserId.ToLower() == normalizedUser &&
                        !r.IsDeleted)
            .Select(r => r.ProgramId)
            .ToListAsync(ct);

        // Programs containing projects where user is assigned
        var projectIdsWithRoles = await _context.ProjectUserRoles
            .AsNoTracking()
            .Where(r => r.TenantId.ToLower() == normalizedTenant &&
                        (r.UserId.ToLower() == normalizedUser || r.UserEmail.ToLower() == normalizedUser) &&
                        !r.IsDeleted)
            .Select(r => r.ProjectId)
            .ToListAsync(ct);

        var programsFromProjects = await _context.Projects
            .AsNoTracking()
            .Where(p => projectIdsWithRoles.Contains(p.Id) && p.ProgramId.HasValue && !p.IsDeleted)
            .Select(p => p.ProgramId!.Value)
            .ToListAsync(ct);

        return directlyAssignedProgramIds
            .Concat(programsFromProjects)
            .Distinct()
            .ToList();
    }
}
