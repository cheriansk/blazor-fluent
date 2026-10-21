using BlazorFluent.Core.Contracts;

using BlazorFluent.Core.Extensions;

using BlazorFluent.Shared.DataListTypes;

using BlazorFluent.Persistence.Context;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Logging;



namespace BlazorFluent.Persistence.Services;



/// <summary>

/// Authoritative Zero Trust implementation of hierarchical security and separation of duties:

/// App Admin -> Tenant Admin -> Program Admin -> Project Admin.

/// Queries PostgreSQL directly with live AsNoTracking execution and cryptographic HMAC verification.

/// </summary>

public class HierarchyAuthorizationService : IHierarchyAuthorizationService

{

    private readonly AppDbContext _context;

    private readonly ICurrentUser _currentUser;

    private readonly IAuditService _auditService;

    private readonly IConfiguration _configuration;

    private readonly ILogger<HierarchyAuthorizationService> _logger;



    public HierarchyAuthorizationService(

        AppDbContext context,

        ICurrentUser currentUser,

        IAuditService auditService,

        IConfiguration configuration,

        ILogger<HierarchyAuthorizationService> logger)

    {

        _context = context;

        _currentUser = currentUser;

        _auditService = auditService;

        _configuration = configuration;

        _logger = logger;

    }



    public async Task<bool> IsTenantAdminAsync(Guid tenantId, string userId, CancellationToken ct = default)

    {

        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;



        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        // Check explicit TenantUser membership

        var tenantUser = await _context.TenantUsers

            .AsNoTracking()

            .FirstOrDefaultAsync(u => u.TenantId == tenantId &&

                                      ((hasGuid && u.UserEntityId == userGuid) || u.User!.Email.ToLower() == normalizedUser) &&

                                      u.Role == TenantRoleEnum.TenantAdmin &&

                                      !u.IsDeleted, ct);



        if (tenantUser != null)

        {

            if (!tenantUser.IsActive) return false;



            var integritySecret = _configuration["Security:IntegritySecret"];

            if (!string.IsNullOrWhiteSpace(integritySecret) && !tenantUser.VerifyIntegritySignature(integritySecret))

            {

                await _auditService.LogSecurityEventAsync(

                    "TamperedTenantAdminDetected",

                    AuditSeverityEnum.Critical,

                    $"TenantUser {tenantUser.Id} for user '{userId}' in tenant '{tenantId}' failed HMAC integrity verification. Administrative privilege denied.",

                    ct);

                return false;

            }



            return true;

        }



        return false;

    }



    public async Task<bool> IsTenantAdminAsync(string tenantId, string userId, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId)) return false;



        if (Guid.TryParse(tenantId.Trim(), out var guidTenant))

        {

            return await IsTenantAdminAsync(guidTenant, userId, ct);

        }



        var normalizedSlug = tenantId.Trim().ToLowerInvariant();

        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == normalizedSlug && !t.IsDeleted, ct);

        if (tenant == null) return false;



        return await IsTenantAdminAsync(tenant.Id, userId, ct);

    }



    public async Task<bool> IsProgramAdminAsync(Guid programId, string userId, CancellationToken ct = default)

    {

        if (programId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;



        var program = await _context.Programs

            .AsNoTracking()

            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);



        if (program == null) return false;



        // Parent Tenant Admin has full administrative governance rights over child programs

        if (await IsTenantAdminAsync(program.TenantId, userId, ct)) return true;



        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        var programRole = await _context.ProgramUserRoles

            .AsNoTracking()

            .FirstOrDefaultAsync(r => r.ProgramId == programId &&

                                      ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                                      r.Role == ProgramRoleEnum.Admin &&

                                      !r.IsDeleted, ct);



        if (programRole != null)

        {

            var integritySecret = _configuration["Security:IntegritySecret"];

            if (!string.IsNullOrWhiteSpace(integritySecret) && !programRole.VerifyIntegritySignature(integritySecret))

            {

                await _auditService.LogSecurityEventAsync(

                    "TamperedProgramAdminDetected",

                    AuditSeverityEnum.Critical,

                    $"ProgramUserRole {programRole.Id} for user '{userId}' on program '{programId}' failed HMAC integrity verification. Administrative privilege denied.",

                    ct);

                return false;

            }



            return true;

        }



        return false;

    }



    public async Task<bool> IsProjectAdminAsync(Guid projectId, string userId, CancellationToken ct = default)

    {

        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;



        var project = await _context.Projects

            .AsNoTracking()

            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);



        if (project == null) return false;



        // 1. Tenant Admin of the owning tenant has full access

        if (await IsTenantAdminAsync(project.TenantId, userId, ct)) return true;



        // 2. Parent Program Admin has full access

        if (project.ProgramId.HasValue && await IsProgramAdminAsync(project.ProgramId.Value, userId, ct)) return true;



        // 3. Project Admin role assignment

        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        var projectRole = await _context.ProjectUserRoles

            .AsNoTracking()

            .FirstOrDefaultAsync(r => r.ProjectId == projectId &&

                                      ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                                      r.Role == ProjectRoleEnum.Admin &&

                                      !r.IsDeleted, ct);



        if (projectRole != null)

        {

            var integritySecret = _configuration["Security:IntegritySecret"];

            if (!string.IsNullOrWhiteSpace(integritySecret) && !projectRole.VerifyIntegritySignature(integritySecret))

            {

                await _auditService.LogSecurityEventAsync(

                    "TamperedProjectAdminDetected",

                    AuditSeverityEnum.Critical,

                    $"ProjectUserRole {projectRole.Id} for user '{userId}' on project '{projectId}' failed HMAC integrity verification. Administrative privilege denied.",

                    ct);

                return false;

            }



            return true;

        }



        return false;

    }



    public async Task<bool> CanViewTenantSettingsAsync(Guid tenantId, string userId, CancellationToken ct = default)

    {

        if (_currentUser.IsRootAdmin) return true;

        if (await IsTenantAdminAsync(tenantId, userId, ct)) return true;



        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        return await _context.ProgramUserRoles

            .AsNoTracking()

            .AnyAsync(r => r.TenantId == tenantId &&

                           ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                           r.Role == ProgramRoleEnum.Admin &&

                           !r.IsDeleted, ct);

    }



    public async Task<bool> CanViewTenantSettingsAsync(string tenantId, string userId, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(tenantId)) return false;

        if (Guid.TryParse(tenantId.Trim(), out var guidTenant)) return await CanViewTenantSettingsAsync(guidTenant, userId, ct);



        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == tenantId.Trim().ToLowerInvariant() && !t.IsDeleted, ct);

        if (tenant == null) return false;

        return await CanViewTenantSettingsAsync(tenant.Id, userId, ct);

    }



    public async Task<bool> CanEditTenantSettingsAsync(Guid tenantId, string userId, CancellationToken ct = default)

    {

        if (_currentUser.IsRootAdmin) return true;

        return await IsTenantAdminAsync(tenantId, userId, ct);

    }



    public async Task<bool> CanEditTenantSettingsAsync(string tenantId, string userId, CancellationToken ct = default)

    {

        if (_currentUser.IsRootAdmin) return true;

        return await IsTenantAdminAsync(tenantId, userId, ct);

    }



    public async Task<bool> CanAccessTenantAsync(Guid tenantId, string userId, CancellationToken ct = default)

    {

        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;



        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        // 1. Strict Tenant Roster Gate: Check active TenantUser record

        var tenantUser = await _context.TenantUsers

            .AsNoTracking()

            .FirstOrDefaultAsync(u => u.TenantId == tenantId &&

                                      ((hasGuid && u.UserEntityId == userGuid) || u.User!.Email.ToLower() == normalizedUser) &&

                                      !u.IsDeleted, ct);



        if (tenantUser != null)

        {

            if (!tenantUser.IsActive) return false; // Explicitly deactivated by Tenant Admin!



            var integritySecret = _configuration["Security:IntegritySecret"];

            if (!string.IsNullOrWhiteSpace(integritySecret) && !tenantUser.VerifyIntegritySignature(integritySecret))

            {

                await _auditService.LogSecurityEventAsync(

                    "TamperedTenantUserDetected",

                    AuditSeverityEnum.Critical,

                    $"TenantUser {tenantUser.Id} for user '{userId}' in tenant '{tenantId}' failed HMAC integrity verification. Access denied.",

                    ct);

                return false;

            }



            return true;

        }



        if (_currentUser.IsRootAdmin)

        {

            return true;

        }



        return false;

    }



    public async Task<bool> CanAccessTenantAsync(string tenantId, string userId, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId)) return false;

        if (Guid.TryParse(tenantId.Trim(), out var guidTenant)) return await CanAccessTenantAsync(guidTenant, userId, ct);



        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == tenantId.Trim().ToLowerInvariant() && !t.IsDeleted, ct);

        if (tenant == null) return false;

        return await CanAccessTenantAsync(tenant.Id, userId, ct);

    }



    public async Task<bool> CanAccessProgramAsync(Guid programId, string userId, CancellationToken ct = default)

    {

        if (programId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;



        var program = await _context.Programs

            .AsNoTracking()

            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);



        if (program == null) return false;



        // Tenant Admin can access all programs in the tenant

        if (await IsTenantAdminAsync(program.TenantId, userId, ct)) return true;



        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        // Explicit Program Role

        var programRole = await _context.ProgramUserRoles

            .AsNoTracking()

            .FirstOrDefaultAsync(r => r.ProgramId == programId &&

                                      ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                                      !r.IsDeleted, ct);



        if (programRole != null)

        {

            var integritySecret = _configuration["Security:IntegritySecret"];

            if (!string.IsNullOrWhiteSpace(integritySecret) && !programRole.VerifyIntegritySignature(integritySecret))

            {

                await _auditService.LogSecurityEventAsync(

                    "TamperedProgramUserRoleDetected",

                    AuditSeverityEnum.Critical,

                    $"ProgramUserRole {programRole.Id} for user '{userId}' on program '{programId}' failed HMAC integrity verification. Access denied.",

                    ct);

                return false;

            }



            return true;

        }



        // Member of any project under this program

        var projectIdsInProgram = await _context.Projects

            .AsNoTracking()

            .Where(p => p.ProgramId == programId && !p.IsDeleted)

            .Select(p => p.Id)

            .ToListAsync(ct);



        return await _context.ProjectUserRoles

            .AsNoTracking()

            .AnyAsync(r => projectIdsInProgram.Contains(r.ProjectId) &&

                           ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                           !r.IsDeleted, ct);

    }



    public async Task<bool> CanAccessProjectAsync(Guid projectId, string userId, CancellationToken ct = default)

    {

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

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        var projectRole = await _context.ProjectUserRoles

            .AsNoTracking()

            .FirstOrDefaultAsync(r => r.ProjectId == projectId &&

                                      ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                                      !r.IsDeleted, ct);



        if (projectRole != null)

        {

            var integritySecret = _configuration["Security:IntegritySecret"];

            if (!string.IsNullOrWhiteSpace(integritySecret) && !projectRole.VerifyIntegritySignature(integritySecret))

            {

                await _auditService.LogSecurityEventAsync(

                    "TamperedProjectUserRoleDetected",

                    AuditSeverityEnum.Critical,

                    $"ProjectUserRole {projectRole.Id} for user '{userId}' on project '{projectId}' failed HMAC integrity verification. Access denied.",

                    ct);

                return false;

            }



            return true;

        }



        return false;

    }



    public async Task<IReadOnlyList<Guid>> GetAccessibleProgramIdsAsync(Guid tenantId, string userId, CancellationToken ct = default)

    {

        if (tenantId == Guid.Empty) return [];



        if (await IsTenantAdminAsync(tenantId, userId, ct))

        {

            return await _context.Programs

                .AsNoTracking()

                .Where(p => p.TenantId == tenantId && !p.IsDeleted)

                .Select(p => p.Id)

                .ToListAsync(ct);

        }



        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        var directlyAssignedProgramIds = await _context.ProgramUserRoles

            .AsNoTracking()

            .Where(r => r.TenantId == tenantId &&

                        ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                        !r.IsDeleted)

            .Select(r => r.ProgramId)

            .ToListAsync(ct);



        var projectIdsWithRoles = await _context.ProjectUserRoles

            .AsNoTracking()

            .Where(r => r.TenantId == tenantId &&

                        ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

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



    public async Task<IReadOnlyList<Guid>> GetAccessibleProgramIdsAsync(string tenantId, string userId, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(tenantId)) return [];

        if (Guid.TryParse(tenantId.Trim(), out var guidTenant)) return await GetAccessibleProgramIdsAsync(guidTenant, userId, ct);



        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == tenantId.Trim().ToLowerInvariant() && !t.IsDeleted, ct);

        if (tenant == null) return [];

        return await GetAccessibleProgramIdsAsync(tenant.Id, userId, ct);

    }



    public async Task<ProjectRoleEnum?> GetEffectiveProjectRoleAsync(Guid projectId, string? userId = null, CancellationToken ct = default)

    {

        if (projectId == Guid.Empty) return null;



        var targetUserId = !string.IsNullOrWhiteSpace(userId)

            ? userId

            : (!string.IsNullOrWhiteSpace(_currentUser.UserId) ? _currentUser.UserId : _currentUser.Email);



        if (string.IsNullOrWhiteSpace(targetUserId)) return null;



        var project = await _context.Projects

            .AsNoTracking()

            .Where(p => p.Id == projectId && !p.IsDeleted)

            .Select(p => new { p.TenantId, p.ProgramId })

            .FirstOrDefaultAsync(ct);



        if (project == null) return null;



        // Tenant Admin has overarching Admin rights on all child projects

        if (await IsTenantAdminAsync(project.TenantId, targetUserId, ct))

        {

            return ProjectRoleEnum.Admin;

        }



        var normalizedUser = targetUserId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        // 1. Check explicit ProjectUserRole assignment

        var projectRoleEntity = await _context.ProjectUserRoles

            .AsNoTracking()

            .Where(r => r.ProjectId == projectId &&

                        ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                        !r.IsDeleted)

            .FirstOrDefaultAsync(ct);



        if (projectRoleEntity != null)

        {

            var integritySecret = _configuration["Security:IntegritySecret"];

            if (!string.IsNullOrWhiteSpace(integritySecret) && !projectRoleEntity.VerifyIntegritySignature(integritySecret))

            {

                await _auditService.LogSecurityEventAsync(

                    "TamperedProjectUserRoleDetected",

                    AuditSeverityEnum.Critical,

                    $"ProjectUserRole {projectRoleEntity.Id} for user '{targetUserId}' on project '{projectId}' failed HMAC integrity verification. Access denied.",

                    ct);

                return null;

            }



            return projectRoleEntity.Role;

        }



        // 2. Check parent Program membership (The "ReadOnly Floor" rule)

        if (project.ProgramId.HasValue)

        {

            var programRoleEntity = await _context.ProgramUserRoles

                .AsNoTracking()

                .Where(r => r.ProgramId == project.ProgramId.Value &&

                            ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                            !r.IsDeleted)

                .FirstOrDefaultAsync(ct);



            if (programRoleEntity != null)

            {

                var integritySecret = _configuration["Security:IntegritySecret"];

                if (string.IsNullOrWhiteSpace(integritySecret) || programRoleEntity.VerifyIntegritySignature(integritySecret))

                {

                    return ProjectRoleEnum.ReadOnly;

                }

            }

        }



        return null;

    }



    public async Task<bool> HasProjectClearanceAsync(Guid projectId, ProjectRoleEnum minimumTier, string? userId = null, CancellationToken ct = default)

    {

        var effectiveRole = await GetEffectiveProjectRoleAsync(projectId, userId, ct);

        return effectiveRole.HasValue && (int)effectiveRole.Value >= (int)minimumTier;

    }



    public async Task EnsureHasProjectClearanceAsync(Guid projectId, ProjectRoleEnum minimumTier, string? userId = null, CancellationToken ct = default)

    {

        var targetUserId = !string.IsNullOrWhiteSpace(userId)

            ? userId

            : (!string.IsNullOrWhiteSpace(_currentUser.UserId) ? _currentUser.UserId : "anonymous");



        var effectiveRole = await GetEffectiveProjectRoleAsync(projectId, targetUserId, ct);



        if (!effectiveRole.HasValue || (int)effectiveRole.Value < (int)minimumTier)

        {

            _logger.LogWarning(

                "AUDIT [Security]: Project clearance denied. UserId='{UserId}', ProjectId='{ProjectId}', RequiredTier='{RequiredTier}', ActualTier='{ActualTier}'",

                targetUserId, projectId, minimumTier, effectiveRole?.ToString() ?? "None");



            await _auditService.LogSecurityEventAsync(

                "ClearanceDenied",

                AuditSeverityEnum.Warning,

                $"Project clearance denied. Required: {minimumTier}, Actual: {effectiveRole?.ToString() ?? "None"}. ProjectId: {projectId}",

                ct);



            throw new System.Security.SecurityException(

                $"Zero Trust Security Violation: User lacks required clearance ({minimumTier}) for project {projectId}.");

        }

    }



    public async Task<bool> IsUserActiveAndProvisionedAsync(string userId, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(userId)) return false;



        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        var user = await _context.Users

            .AsNoTracking()

            .FirstOrDefaultAsync(u => ((hasGuid && u.Id == userGuid) || u.Email.ToLower() == normalizedUser) && !u.IsDeleted, ct);



        if (user == null || !user.IsActive) return false;



        var integritySecret = _configuration["Security:IntegritySecret"];

        if (!string.IsNullOrWhiteSpace(integritySecret) && !user.VerifyIntegritySignature(integritySecret))

        {

            await _auditService.LogSecurityEventAsync(

                "TamperedUserEntityDetected",

                AuditSeverityEnum.Critical,

                $"UserEntity {user.Id} ({user.Email}) failed HMAC integrity verification. Account disabled.",

                ct);

            return false;

        }



        var now = DateTime.UtcNow;

        if (!user.IsActiveAt(now)) return false;



        var hasProjectRole = await _context.ProjectUserRoles

            .AsNoTracking()

            .AnyAsync(r => ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) && !r.IsDeleted, ct);



        if (hasProjectRole) return true;



        var hasProgramRole = await _context.ProgramUserRoles

            .AsNoTracking()

            .AnyAsync(r => ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) && !r.IsDeleted, ct);



        if (hasProgramRole) return true;



        var hasTenantAdminRole = await _context.TenantUsers

            .AsNoTracking()

            .AnyAsync(u => ((hasGuid && u.UserEntityId == userGuid) || u.User!.Email.ToLower() == normalizedUser) &&

                           u.Role == TenantRoleEnum.TenantAdmin && u.IsActive && !u.IsDeleted, ct);



        return hasTenantAdminRole;

    }



    public Task<bool> CanManageTenantAdminsAsync(Guid tenantId, string userId, CancellationToken ct = default) =>

        CanManageTenantAdminsAsync(tenantId.ToString(), userId, ct);



    public Task<bool> CanManageTenantAdminsAsync(string tenantId, string userId, CancellationToken ct = default)

    {

        // Strictly App Administrators (Root Admins) can assign, modify, or revoke Tenant Admins

        return Task.FromResult(_currentUser.IsRootAdmin);

    }



    public async Task<bool> CanManageProgramAdminsAsync(Guid programId, string userId, CancellationToken ct = default)

    {

        if (programId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;



        var program = await _context.Programs

            .AsNoTracking()

            .FirstOrDefaultAsync(p => p.Id == programId && !p.IsDeleted, ct);



        if (program == null) return false;



        // Tenant Admin of the owning tenant can manage program admins

        if (await IsTenantAdminAsync(program.TenantId, userId, ct)) return true;



        // Existing Program Admin of this program can manage program admins

        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        var programRole = await _context.ProgramUserRoles

            .AsNoTracking()

            .FirstOrDefaultAsync(r => r.ProgramId == programId &&

                                      ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                                      r.Role == ProgramRoleEnum.Admin &&

                                      !r.IsDeleted, ct);



        if (programRole != null)

        {

            var integritySecret = _configuration["Security:IntegritySecret"];

            return string.IsNullOrWhiteSpace(integritySecret) || programRole.VerifyIntegritySignature(integritySecret);

        }



        return false;

    }



    public async Task<bool> CanManageProjectAdminsAsync(Guid projectId, string userId, CancellationToken ct = default)

    {

        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId)) return false;



        var project = await _context.Projects

            .AsNoTracking()

            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);



        if (project == null) return false;



        // 1. Tenant Admin of the owning tenant

        if (await IsTenantAdminAsync(project.TenantId, userId, ct)) return true;



        // 2. Parent Program Admin

        if (project.ProgramId.HasValue && await IsProgramAdminAsync(project.ProgramId.Value, userId, ct)) return true;



        // 3. Existing Project Admin of this project

        var normalizedUser = userId.Trim().ToLowerInvariant();

        var hasGuid = Guid.TryParse(normalizedUser, out var userGuid);



        var projectRole = await _context.ProjectUserRoles

            .AsNoTracking()

            .FirstOrDefaultAsync(r => r.ProjectId == projectId &&

                                      ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&

                                      r.Role == ProjectRoleEnum.Admin &&

                                      !r.IsDeleted, ct);



        if (projectRole != null)

        {

            var integritySecret = _configuration["Security:IntegritySecret"];

            return string.IsNullOrWhiteSpace(integritySecret) || projectRole.VerifyIntegritySignature(integritySecret);

        }



        return false;

    }

}

