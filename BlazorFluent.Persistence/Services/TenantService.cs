using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class TenantService : ITenantService
{
    private const string TenantsCacheKey = "tenants:all";
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ITenantCacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly IRootAdminService _rootAdminService;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        AppDbContext dbContext,
        IAuditService auditService,
        ITenantCacheService cacheService,
        IConfiguration configuration,
        IRootAdminService rootAdminService,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ILogger<TenantService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _cacheService = cacheService;
        _configuration = configuration;
        _rootAdminService = rootAdminService;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantEntity>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        // Zero Trust: Tenant directory access is restricted strictly to designated Root Super-Administrators
        if (!_currentUser.IsRootAdmin)
        {
            _logger.LogWarning("Security Violation: User '{UserId}' ({Email}) attempted to access tenant directory without RootAdmin privileges.",
                _currentUser.UserId, _currentUser.Email);
            return [];
        }

        return await _cacheService.GetGlobalOrCreateAsync(
            TenantsCacheKey,
            async token =>
            {
                return (IReadOnlyList<TenantEntity>)await _dbContext.Tenants
                    .AsNoTracking()
                    .Where(t => t.Slug != "default")
                    .OrderBy(t => t.Name)
                    .ToListAsync(token);
            },
            expiration: TimeSpan.FromMinutes(60),
            cancellationToken: cancellationToken);
    }

    public async Task<Result<TenantEntity>> CreateTenantAsync(
        string slug,
        string displayName,
        IEnumerable<string> internalEmailDomains,
        IEnumerable<string> externalEmailDomains,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsRootAdmin)
            return Result<TenantEntity>.Failure("Access Denied: Only designated root super-administrators can provision new tenants.");

        if (string.IsNullOrWhiteSpace(slug))
            return Result<TenantEntity>.Failure("Slug is required.");

        if (string.Equals(slug.Trim(), "default", StringComparison.OrdinalIgnoreCase))
            return Result<TenantEntity>.Failure("The 'default' slug is reserved for internal system administration.");

        if (string.IsNullOrWhiteSpace(displayName))
            return Result<TenantEntity>.Failure("DisplayName is required.");

        if (endDate < startDate)
            return Result<TenantEntity>.Failure("End date cannot precede start date.");

        var normalizedInternal = (internalEmailDomains ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d =>
            {
                var trimmed = d.Trim().ToLowerInvariant();
                return trimmed.StartsWith('@') ? trimmed : "@" + trimmed;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedExternal = (externalEmailDomains ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d =>
            {
                var trimmed = d.Trim().ToLowerInvariant();
                return trimmed.StartsWith('@') ? trimmed : "@" + trimmed;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedInternal.Count is < 1 or > 5)
            return Result<TenantEntity>.Failure("Between 1 and 5 Internal email domains must be configured.");

        if (normalizedExternal.Count is < 1 or > 5)
            return Result<TenantEntity>.Failure("Between 1 and 5 External email domains must be configured.");

        var overlap = normalizedInternal.Intersect(normalizedExternal, StringComparer.OrdinalIgnoreCase).ToList();
        if (overlap.Any())
            return Result<TenantEntity>.Failure($"Internal and External email domains cannot overlap. Overlapping domain(s): {string.Join(", ", overlap)}.");

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (await _dbContext.Tenants.AnyAsync(t => t.Slug == normalizedSlug, cancellationToken))
        {
            return Result<TenantEntity>.Failure($"A tenant with slug '{normalizedSlug}' already exists.");
        }

        var internalJoined = string.Join(";", normalizedInternal);
        var externalJoined = string.Join(";", normalizedExternal);

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Slug = normalizedSlug,
            Name = displayName.Trim(),
            InternalEmailDomains = internalJoined,
            ExternalEmailDomains = externalJoined,
            IsActive = true,
            StartDate = startDate,
            EndDate = endDate
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict global tenant directory cache across all auto-scaled instances
        await _cacheService.RemoveGlobalAsync(TenantsCacheKey, cancellationToken);

        _logger.LogInformation("Provisioned new tenant: Slug={Slug}, Name={Name}, InternalDomains={InternalDomains}, ExternalDomains={ExternalDomains}, StartDate={StartDate}, EndDate={EndDate}",
            tenant.Slug, tenant.Name, tenant.InternalEmailDomains, tenant.ExternalEmailDomains, tenant.StartDate, tenant.EndDate);

        await _auditService.LogUserActivityAsync(
            $"Created tenant '{tenant.Name}' (Slug: {tenant.Slug})",
            $"Slug: {tenant.Slug}, Internal: {tenant.InternalEmailDomains}, External: {tenant.ExternalEmailDomains}, StartDate: {tenant.StartDate:yyyy-MM-dd}, EndDate: {tenant.EndDate:yyyy-MM-dd}",
            cancellationToken);

        return Result<TenantEntity>.Success(tenant);
    }

    public Task<Result<TenantEntity>> CreateTenantAsync(
        string slug,
        string displayName,
        string internalEmailDomain,
        string externalEmailDomain,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return CreateTenantAsync(slug, displayName, [internalEmailDomain], [externalEmailDomain], startDate, endDate, cancellationToken);
    }

    public async Task<IReadOnlyList<UserEntity>> GetTenantUsersAsync(string tenantSlug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        // Zero Trust: Non-root users can only query users within their own active tenant
        if (!_currentUser.IsRootAdmin && !string.Equals(normalizedSlug, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to access users of tenant '{TenantSlug}' outside their active tenant '{ActiveTenant}'.",
                _currentUser.UserId, tenantSlug, _tenantContext.TenantId);
            return [];
        }

        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.DefaultTenantId == normalizedSlug && !u.IsDeleted)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<UserEntity>> AddTenantUserAsync(
        string tenantSlug,
        string fullName,
        string email,
        DateTime startDate,
        DateTime endDate,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        // Zero Trust: Non-root users can only add users to their own active tenant
        if (!_currentUser.IsRootAdmin && !string.Equals(normalizedSlug, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to add user to tenant '{TenantSlug}' outside their active tenant '{ActiveTenant}'.",
                _currentUser.UserId, tenantSlug, _tenantContext.TenantId);
            return Result<UserEntity>.Failure("Access Denied: You can only manage users within your active tenant.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
            return Result<UserEntity>.Failure("Full Name is required.");

        if (string.IsNullOrWhiteSpace(email))
            return Result<UserEntity>.Failure("Email address is required.");

        if (endDate < startDate)
            return Result<UserEntity>.Failure("End date cannot precede start date.");

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (normalizedEmail == "system" || normalizedEmail.Contains("daemon.local"))
            return Result<UserEntity>.Failure("Reserved background system daemon identities cannot be provisioned as user accounts.");

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken);

        if (tenant is null)
            return Result<UserEntity>.Failure($"Tenant '{tenantSlug}' was not found.");

        // Enforce maximum 3 users in the default system root tenant
        if (string.Equals(normalizedSlug, "default", StringComparison.OrdinalIgnoreCase))
        {
            var defaultUserCount = await _dbContext.Users
                .CountAsync(u => u.DefaultTenantId == "default" && !u.IsDeleted, cancellationToken);
            if (defaultUserCount >= 3)
            {
                return Result<UserEntity>.Failure("The default system root tenant cannot exceed a maximum of 3 administrator accounts.");
            }
        }

        // Automatically determine UserType from configured tenant domains
        UserType userType;
        var internalDomains = tenant.GetInternalDomains();
        var externalDomains = tenant.GetExternalDomains();

        if (internalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.CompanyUser;
        }
        else if (externalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.ClientUser;
        }
        else
        {
            return Result<UserEntity>.Failure(
                $"Email '{normalizedEmail}' does not match any configured Internal domain ({string.Join(", ", internalDomains)}) or External domain ({string.Join(", ", externalDomains)}) for tenant '{tenant.Name}'.");
        }

        // Check if user already exists with this email
        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken))
        {
            return Result<UserEntity>.Failure($"A user with email '{normalizedEmail}' already exists.");
        }

        var secretKey = _configuration["Security:IntegritySecret"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.StartsWith("__SET_VIA_"))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: 'Security:IntegritySecret' must be configured in KeyVault, environment, or User Secrets.");
        }
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = fullName.Trim(),
            UserType = userType,
            DefaultTenantId = normalizedSlug,
            IsActive = isActive,
            StartDateUtc = startDate.ToUniversalTime(),
            EndDateUtc = endDate.ToUniversalTime()
        };

        user.RowSignature = user.ComputeIntegritySignature(secretKey);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added user '{Email}' ({UserType}) to tenant '{TenantSlug}'", user.Email, user.UserType, tenantSlug);

        await _auditService.LogUserActivityAsync(
            $"Created tenant user '{user.FullName}' ({user.Email})",
            $"Tenant: {tenantSlug}, Type: {user.UserType}, Active: {user.IsActive}",
            cancellationToken);

        return Result<UserEntity>.Success(user);
    }

    public async Task<Result<UserEntity>> UpdateTenantUserAsync(
        string tenantSlug,
        Guid userId,
        string fullName,
        string email,
        DateTime startDate,
        DateTime endDate,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        // Zero Trust: Non-root users can only modify users within their own active tenant
        if (!_currentUser.IsRootAdmin && !string.Equals(normalizedSlug, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to modify user in tenant '{TenantSlug}' outside their active tenant '{ActiveTenant}'.",
                _currentUser.UserId, tenantSlug, _tenantContext.TenantId);
            return Result<UserEntity>.Failure("Access Denied: You can only manage users within your active tenant.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
            return Result<UserEntity>.Failure("Full Name is required.");

        if (string.IsNullOrWhiteSpace(email))
            return Result<UserEntity>.Failure("Email address is required.");

        if (endDate < startDate)
            return Result<UserEntity>.Failure("End date cannot precede start date.");

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (normalizedEmail == "system" || normalizedEmail.Contains("daemon.local"))
            return Result<UserEntity>.Failure("Reserved background system daemon identities cannot be provisioned as user accounts.");

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken);

        if (tenant is null)
            return Result<UserEntity>.Failure($"Tenant '{tenantSlug}' was not found.");

        var user = await _dbContext.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.DefaultTenantId == normalizedSlug && !u.IsDeleted, cancellationToken);

        if (user is null)
            return Result<UserEntity>.Failure($"User was not found in tenant '{tenantSlug}'.");

        var internalDomains = tenant.GetInternalDomains();
        var externalDomains = tenant.GetExternalDomains();

        UserType userType;
        if (internalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.CompanyUser;
        }
        else if (externalDomains.Any(d => normalizedEmail.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
        {
            userType = UserType.ClientUser;
        }
        else
        {
            return Result<UserEntity>.Failure(
                $"Email '{normalizedEmail}' does not match any configured Internal domain ({string.Join(", ", internalDomains)}) or External domain ({string.Join(", ", externalDomains)}) for tenant '{tenant.Name}'.");
        }

        // If email changed, check uniqueness
        if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != userId && !u.IsDeleted, cancellationToken))
            {
                return Result<UserEntity>.Failure($"A user with email '{normalizedEmail}' already exists.");
            }
        }

        user.FullName = fullName.Trim();
        user.Email = normalizedEmail;
        user.UserType = userType;
        user.StartDateUtc = startDate.ToUniversalTime();
        user.EndDateUtc = endDate.ToUniversalTime();
        user.IsActive = isActive;

        var secretKey = _configuration["Security:IntegritySecret"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.StartsWith("__SET_VIA_"))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: 'Security:IntegritySecret' must be configured in KeyVault, environment, or User Secrets.");
        }
        user.RowSignature = user.ComputeIntegritySignature(secretKey);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated user '{Email}' ({UserType}, Active={Active}) in tenant '{TenantSlug}'", user.Email, user.UserType, user.IsActive, tenantSlug);

        await _auditService.LogUserActivityAsync(
            $"Updated tenant user '{user.FullName}' ({user.Email})",
            $"Tenant: {tenantSlug}, Type: {user.UserType}, Active: {user.IsActive}, StartDate: {user.StartDateUtc:yyyy-MM-dd}, EndDate: {user.EndDateUtc:yyyy-MM-dd}",
            cancellationToken);

        return Result<UserEntity>.Success(user);
    }

    public async Task<Result> UpdateTenantStatusAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsRootAdmin)
            return Result.Failure("Access Denied: Only designated root super-administrators can modify tenants.");

        var tenant = await _dbContext.Tenants
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null) return Result.Failure("Tenant not found.");

        if (string.Equals(tenant.Slug, "default", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("The default system root anchor tenant cannot be deactivated or modified.");

        tenant.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict global tenant directory cache across all auto-scaled instances
        await _cacheService.RemoveGlobalAsync(TenantsCacheKey, cancellationToken);

        // If deactivated, purge all tenant session/circuit cache entries across all nodes
        if (!isActive)
        {
            await _cacheService.InvalidateTenantAsync(tenant.Slug, cancellationToken);
            await _cacheService.InvalidateTenantAsync(tenant.Id.ToString(), cancellationToken);
        }

        _logger.LogInformation("Updated tenant status: Slug={Slug}, IsActive={IsActive}", tenant.Slug, isActive);

        await _auditService.LogSecurityEventAsync(
            $"Tenant status changed for '{tenant.Name}' to {(isActive ? "Active" : "Inactive")}",
            isActive ? AuditSeverity.Information : AuditSeverity.Warning,
            $"TenantId: {tenant.Id}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UpdateTenantDatesAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsRootAdmin)
            return Result.Failure("Access Denied: Only designated root super-administrators can modify tenants.");

        if (endDate < startDate)
            return Result.Failure("End date cannot precede start date.");

        var tenant = await _dbContext.Tenants
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null) return Result.Failure("Tenant not found.");

        if (string.Equals(tenant.Slug, "default", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("The default system root anchor tenant cannot be modified.");

        tenant.StartDate = startDate;
        tenant.EndDate = endDate;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Evict global tenant directory cache across all auto-scaled instances
        await _cacheService.RemoveGlobalAsync(TenantsCacheKey, cancellationToken);

        _logger.LogInformation("Updated tenant dates: Slug={Slug}, StartDate={StartDate}, EndDate={EndDate}",
            tenant.Slug, startDate, endDate);

        await _auditService.LogUserActivityAsync(
            $"Updated contract dates for tenant '{tenant.Name}'",
            $"StartDate: {startDate:yyyy-MM-dd}, EndDate: {endDate:yyyy-MM-dd}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<IReadOnlyList<ProjectEntity>> GetTenantProjectsAsync(string tenantSlug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        // Zero Trust: Non-root users can only query projects within their active tenant
        if (!_currentUser.IsRootAdmin && !string.Equals(normalizedSlug, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to access projects of tenant '{TenantSlug}' outside their active tenant '{ActiveTenant}'.",
                _currentUser.UserId, tenantSlug, _tenantContext.TenantId);
            return [];
        }

        return await _dbContext.Projects
            .AsNoTracking()
            .Where(p => p.TenantId == normalizedSlug)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<ProjectEntity>> CreateTenantProjectAsync(
        string tenantSlug,
        string name,
        string shortCode,
        string? location,
        DateTime? startDate,
        DateTime? endDate,
        string? scopeSummary,
        ProjectStatus status,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        // Zero Trust: Non-root users can only create projects within their active tenant
        if (!_currentUser.IsRootAdmin && !string.Equals(normalizedSlug, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to create project in tenant '{TenantSlug}' outside their active tenant '{ActiveTenant}'.",
                _currentUser.UserId, tenantSlug, _tenantContext.TenantId);
            return Result<ProjectEntity>.Failure("Access Denied: You cannot create projects for other tenants.");
        }

        if (string.IsNullOrWhiteSpace(name))
            return Result<ProjectEntity>.Failure("Project Name is required.");

        if (string.IsNullOrWhiteSpace(shortCode))
            return Result<ProjectEntity>.Failure("Project Short Code is required.");

        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
            return Result<ProjectEntity>.Failure("End date cannot precede start date.");

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken);

        if (tenant is null)
            return Result<ProjectEntity>.Failure($"Tenant '{tenantSlug}' was not found.");

        var trimmedName = name.Trim();
        var trimmedCode = shortCode.Trim();

        var duplicate = await _dbContext.Projects
            .AnyAsync(p => p.TenantId == normalizedSlug &&
                           !p.IsDeleted &&
                           (p.Name.ToLower() == trimmedName.ToLower() || p.ShortCode.ToLower() == trimmedCode.ToLower()),
                      cancellationToken);

        if (duplicate)
            return Result<ProjectEntity>.Failure($"A project with name '{trimmedName}' or short code '{trimmedCode}' already exists for this tenant.");

        var project = new ProjectEntity
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            ShortCode = trimmedCode,
            Location = location?.Trim(),
            TentativeStartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : null,
            TentativeEndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : null,
            ScopeSummary = scopeSummary?.Trim(),
            Description = scopeSummary?.Trim() ?? string.Empty,
            Status = status,
            IsActive = status == ProjectStatus.Active || status == ProjectStatus.New,
            TenantEntityId = tenant.Id,
            TenantId = normalizedSlug
        };

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created project '{ProjectName}' ({ShortCode}) in tenant '{TenantSlug}'", project.Name, project.ShortCode, tenantSlug);

        await _auditService.LogUserActivityAsync(
            $"Created tenant project '{project.Name}' ({project.ShortCode})",
            $"Tenant: {tenantSlug}, Status: {project.Status}, Location: {project.Location}",
            cancellationToken);

        if (_tenantContext.IsHost && !string.Equals(normalizedSlug, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            await _auditService.LogSecurityEventAsync(
                "HostCrossTenantAction",
                AuditSeverity.Warning,
                $"[Host Privilege] Host user '{_currentUser.UserId}' created project '{project.Name}' in tenant '{normalizedSlug}'.",
                cancellationToken);
        }

        return Result<ProjectEntity>.Success(project);
    }

    public async Task<Result<ProjectEntity>> UpdateTenantProjectAsync(
        string tenantSlug,
        Guid projectId,
        string name,
        string shortCode,
        string? location,
        DateTime? startDate,
        DateTime? endDate,
        string? scopeSummary,
        ProjectStatus status,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<ProjectEntity>.Failure("Project Name is required.");

        if (string.IsNullOrWhiteSpace(shortCode))
            return Result<ProjectEntity>.Failure("Project Short Code is required.");

        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
            return Result<ProjectEntity>.Failure("End date cannot precede start date.");

        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();

        var project = await _dbContext.Projects
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == normalizedSlug, cancellationToken);

        if (project is null)
            return Result<ProjectEntity>.Failure($"Project was not found in tenant '{tenantSlug}'.");

        var trimmedName = name.Trim();
        var trimmedCode = shortCode.Trim();

        var duplicate = await _dbContext.Projects
            .AnyAsync(p => p.TenantId == normalizedSlug && p.Id != projectId &&
                           !p.IsDeleted &&
                           (p.Name.ToLower() == trimmedName.ToLower() || p.ShortCode.ToLower() == trimmedCode.ToLower()),
                      cancellationToken);

        if (duplicate)
            return Result<ProjectEntity>.Failure($"Another project with name '{trimmedName}' or short code '{trimmedCode}' already exists for this tenant.");

        project.Name = trimmedName;
        project.ShortCode = trimmedCode;
        project.Location = location?.Trim();
        project.TentativeStartDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : null;
        project.TentativeEndDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : null;
        project.ScopeSummary = scopeSummary?.Trim();
        if (!string.IsNullOrWhiteSpace(scopeSummary))
        {
            project.Description = scopeSummary.Trim();
        }
        project.Status = status;
        project.IsActive = status == ProjectStatus.Active || status == ProjectStatus.New;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated project '{ProjectName}' ({ShortCode}) in tenant '{TenantSlug}'", project.Name, project.ShortCode, tenantSlug);

        await _auditService.LogUserActivityAsync(
            $"Updated tenant project '{project.Name}' ({project.ShortCode})",
            $"Tenant: {tenantSlug}, Status: {project.Status}, Location: {project.Location}",
            cancellationToken);

        if (_tenantContext.IsHost && !string.Equals(normalizedSlug, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            await _auditService.LogSecurityEventAsync(
                "HostCrossTenantAction",
                AuditSeverity.Warning,
                $"[Host Privilege] Host user '{_currentUser.UserId}' updated project '{project.Name}' in tenant '{normalizedSlug}'.",
                cancellationToken);
        }

        return Result<ProjectEntity>.Success(project);
    }
}
