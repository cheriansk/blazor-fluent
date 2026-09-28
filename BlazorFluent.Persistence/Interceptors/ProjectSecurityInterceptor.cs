using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Delegates;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// Fail-closed EF Core SaveChangesInterceptor that enforces project-level write security.
/// Automatically intercepts any Added, Modified, or Deleted entities implementing <see cref="IProjectScopedEntity"/>
/// and verifies that the active user possesses write permissions (Dev, QA, Admin) in that project.
/// </summary>
public class ProjectSecurityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly HybridCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectSecurityInterceptor> _logger;

    public ProjectSecurityInterceptor(
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        HybridCache cache,
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectSecurityInterceptor>? logger = null)
    {
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectSecurityInterceptor>.Instance;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ValidateProjectSecurity(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectSecurity(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ValidateProjectSecurity(DbContext? context)
    {
        if (context is null) return;

        // System processes, migrations, and Host/Tenant SuperAdmins have bypass
        if (_tenantContext.IsHost || _currentUser.IsInRole("Admin") || _currentUser.UserId == "system")
        {
            return;
        }

        var modifiedProjectEntities = context.ChangeTracker.Entries<IProjectScopedEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (modifiedProjectEntities.Count == 0)
        {
            return;
        }

        var userId = _currentUser.UserId ?? "anonymous";
        var tenantId = _tenantContext.TenantId ?? "no_tenant";

        // Group by ProjectId to minimize verification lookups
        var projectIds = modifiedProjectEntities
            .Select(e => e.Entity.ProjectId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        foreach (var projectId in projectIds)
        {
            var role = ResolveRoleForProject(tenantId, userId, projectId);
            if (role is null || !role.Value.CanWrite())
            {
                _logger.LogCritical(
                    "SECURITY INTERCEPTOR VIOLATION: User '{UserId}' lacks write permissions on project '{ProjectId}' (Current Role: {Role}). Write operation terminated.",
                    userId, projectId, role?.ToString() ?? "None");

                throw new UnauthorizedAccessException(
                    $"Fail-Closed Security Interceptor: User '{userId}' does not have write permissions for project '{projectId}'. Database write blocked.");
            }
        }
    }

    private ProjectRole? ResolveRoleForProject(string tenantId, string userId, Guid projectId)
    {
        var cacheKey = $"proj_role_{tenantId}_{userId}_{projectId}";

        // Synchronous cache lookup or isolated scope query
        // Using an isolated DbContext avoids concurrency issues with the currently saving context
        using var scope = _scopeFactory.CreateScope();
        var isolatedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleEntity = isolatedDb.ProjectUserRoles
            .AsNoTracking()
            .FirstOrDefault(r => r.ProjectId == projectId && r.UserId == userId && !r.IsDeleted);

        return roleEntity?.Role;
    }
}
