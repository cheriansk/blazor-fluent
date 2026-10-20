using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Security;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BlazorFluent.Core.Utilities;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// Fail-closed EF Core SaveChangesInterceptor that enforces per-project RBAC write gates
/// directly at the database persistence boundary.
/// Any attempt to add, modify, or delete an <see cref="IProjectScopedEntity"/> without
/// at least <see cref="ProjectRoleEnum.Contribute"/> or <see cref="ProjectRoleEnum.Contribute"/> permissions
/// immediately aborts the transaction.
/// </summary>
public class ProjectSecurityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectSecurityInterceptor> _logger;

    public ProjectSecurityInterceptor(
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectSecurityInterceptor> logger)
    {
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EnforceProjectWritePermissions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnforceProjectWritePermissions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceProjectWritePermissions(DbContext? context)
    {
        if (context is null) return;

        // Verified internal background system daemons bypass per-project user gates.
        // App Admins (Root Admins) are subject to project zero-trust boundaries and cannot write business data unless granted a project role.
        if (_currentUser.IsSystemDaemon ||
            SystemIdentityUtility.IsSystemDaemon(_currentUser.UserId) ||
            SystemIdentityUtility.IsSystemDaemon(_currentUser.Email))
        {
            return;
        }

        var scopedEntries = context.ChangeTracker
            .Entries<IProjectScopedEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (scopedEntries.Count == 0) return;

        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !_currentUser.IsAuthenticated)
        {
            _logger.LogCritical(
                "SECURITY INTERCEPTOR VIOLATION: Unauthenticated user attempted to mutate {Count} project-scoped entities.",
                scopedEntries.Count);
            throw new UnauthorizedAccessException("Fail-Closed Security Interceptor: Unauthenticated write to project-scoped entity is forbidden.");
        }

        // Group by distinct ProjectId to minimize role lookups per SaveChanges batch
        var distinctProjectIds = scopedEntries
            .Select(e => e.Entity.ProjectId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        foreach (var projectId in distinctProjectIds)
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

    private ProjectRoleEnum? ResolveRoleForProject(string userId, Guid projectId)
    {
        // Isolated scope query avoids DbContext concurrency collisions
        using var scope = _scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        currentUser?.SetSystemDaemon("ProjectSecurityInterceptor");
        var isolatedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hasGuid = Guid.TryParse(userId, out var userGuid);
        var normalizedUser = userId.Trim().ToLowerInvariant();

        var roleEntity = isolatedDb.ProjectUserRoles
            .AsNoTracking()
            .FirstOrDefault(r => r.ProjectId == projectId &&
                                 ((hasGuid && r.UserEntityId == userGuid) || r.User!.Email.ToLower() == normalizedUser) &&
                                 !r.IsDeleted);

        return roleEntity?.Role;
    }
}
