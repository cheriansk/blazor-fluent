using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Notifications;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// Fail-closed singleton EF Core SaveChangesInterceptor that enforces project-level write security.
/// Automatically intercepts any Added, Modified, or Deleted entities implementing <see cref="IProjectScopedEntity"/>
/// and verifies that the active user possesses write permissions (Dev, QA, Admin) in that project.
/// Dynamically extracts user and tenant context from AppDbContext per invocation.
/// </summary>
public class ProjectSecurityInterceptor : SaveChangesInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectSecurityInterceptor> _logger;

    public ProjectSecurityInterceptor(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectSecurityInterceptor>? logger = null)
    {
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

        var appDb = context as AppDbContext;
        var currentUser = appDb?.CurrentUser ?? new DefaultCurrentUser();
        var tenantContext = appDb?.TenantContext ?? new TenantContext();

        // System daemon workers, migrations, and Root SuperAdmins have bypass
        if (currentUser.IsRootAdmin || currentUser.IsSystemDaemon)
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

        var userId = currentUser.UserId ?? "anonymous";
        var tenantId = tenantContext.TenantId ?? "no_tenant";

        // Allow users to update their own personal notifications (e.g., mark as read)
        var projectEntitiesToValidate = modifiedProjectEntities
            .Where(e => !(e.Entity is NotificationEntity notif && notif.UserId == userId))
            .ToList();

        if (projectEntitiesToValidate.Count == 0)
        {
            return;
        }

        // Group by ProjectId to minimize verification lookups
        var projectIds = projectEntitiesToValidate
            .Select(e => e.Entity.ProjectId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        foreach (var projectId in projectIds)
        {
            var role = ResolveRoleForProject(userId, projectId);
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

    private ProjectRole? ResolveRoleForProject(string userId, Guid projectId)
    {
        // Isolated scope query avoids DbContext concurrency collisions
        using var scope = _scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        currentUser?.SetSystemDaemon("ProjectSecurityInterceptor");
        var isolatedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleEntity = isolatedDb.ProjectUserRoles
            .AsNoTracking()
            .FirstOrDefault(r => r.ProjectId == projectId && r.UserId == userId && !r.IsDeleted);

        return roleEntity?.Role;
    }
}
