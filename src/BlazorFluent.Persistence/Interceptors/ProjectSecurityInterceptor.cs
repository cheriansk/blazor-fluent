using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Security;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Interceptors;

/// <summary>
/// Defense-in-depth security interceptor enforcing Per-Project Role-Based Access Control (RBAC).
/// Intercepts entity mutations (Insert, Update, Delete) on entities implementing <see cref="IProjectScopedEntity"/>
/// and verifies that the current user possesses write permissions (<see cref="ProjectRoleEnumExtensions.CanWrite"/>)
/// within the target project scope before any changes are committed to PostgreSQL.
/// </summary>
public class ProjectSecurityInterceptor : SaveChangesInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectSecurityInterceptor> _logger;

    public ProjectSecurityInterceptor(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectSecurityInterceptor> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EnforceProjectWritePermissions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await EnforceProjectWritePermissionsAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceProjectWritePermissions(DbContext? context)
    {
        if (context is null) return;

        var currentUser = (context as AppDbContext)?.CurrentUser ?? new DefaultCurrentUser();

        // Verified internal background system daemons bypass per-project user gates.
        if (currentUser.IsSystemDaemon) return;

        // Host / Root Admin users bypass per-project member gates
        if (currentUser.IsRootAdmin) return;

        var scopedEntries = context.ChangeTracker
            .Entries<IProjectScopedEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (scopedEntries.Count == 0) return;

        var userId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !currentUser.IsAuthenticated)
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
                    $"Fail-Closed Security Interceptor: User '{userId}' does not have write permissions for project '{projectId}'. Database write aborted.");
            }
        }
    }

    private async Task EnforceProjectWritePermissionsAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null) return;

        var currentUser = (context as AppDbContext)?.CurrentUser ?? new DefaultCurrentUser();

        // Verified internal background system daemons bypass per-project user gates.
        if (currentUser.IsSystemDaemon) return;

        // Host / Root Admin users bypass per-project member gates
        if (currentUser.IsRootAdmin) return;

        var scopedEntries = context.ChangeTracker
            .Entries<IProjectScopedEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (scopedEntries.Count == 0) return;

        var userId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !currentUser.IsAuthenticated)
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
            var role = await ResolveRoleForProjectAsync(userId, projectId, ct);
            if (role is null || !role.Value.CanWrite())
            {
                _logger.LogCritical(
                    "SECURITY INTERCEPTOR VIOLATION: User '{UserId}' lacks write permissions on project '{ProjectId}' (Current Role: {Role}). Write operation terminated.",
                    userId, projectId, role?.ToString() ?? "None");

                throw new UnauthorizedAccessException(
                    $"Fail-Closed Security Interceptor: User '{userId}' does not have write permissions for project '{projectId}'. Database write aborted.");
            }
        }
    }

    /// <summary>
    /// Evaluates effective project permissions within a dedicated transient scope for synchronous calls.
    /// <para>
    /// DESIGN JUSTIFICATION FOR SYNCHRONOUS INVOCATION (.GetAwaiter().GetResult()):
    /// EF Core's ISaveChangesInterceptor defines both SavingChanges (synchronous) and SavingChangesAsync (asynchronous).
    /// When SaveChanges() is invoked synchronously by legacy callers or synchronous transaction pipelines, the interceptor
    /// must synchronously resolve effective roles. Since IHierarchyAuthorizationService operates on in-memory roles
    /// and pre-calculated hierarchy matrices without blocking socket I/O, synchronous resolution is safe from thread starvation.
    /// Asynchronous callers invoke <see cref="ResolveRoleForProjectAsync"/> which is 100% non-blocking.
    /// </para>
    /// </summary>
    private ProjectRoleEnum? ResolveRoleForProject(string userId, Guid projectId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var hierarchyAuth = scope.ServiceProvider.GetService<IHierarchyAuthorizationService>();
            if (hierarchyAuth is null) return null;

            return hierarchyAuth.GetEffectiveProjectRoleAsync(projectId, userId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve project role for user '{UserId}' on project '{ProjectId}' in ProjectSecurityInterceptor.", userId, projectId);
            return null;
        }
    }

    private async Task<ProjectRoleEnum?> ResolveRoleForProjectAsync(string userId, Guid projectId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var hierarchyAuth = scope.ServiceProvider.GetService<IHierarchyAuthorizationService>();
            if (hierarchyAuth is null) return null;

            return await hierarchyAuth.GetEffectiveProjectRoleAsync(projectId, userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve project role for user '{UserId}' on project '{ProjectId}' in ProjectSecurityInterceptor.", userId, projectId);
            return null;
        }
    }
}
