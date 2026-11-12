using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Security;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Interceptors;

/// <summary>
/// Defense-in-depth security interceptor enforcing Multi-Tier Role-Based Access Control (RBAC).
/// Intercepts entity mutations (Insert, Update, Delete) on:
/// 1. Entities implementing IProjectScopedEntity.
/// 2. Entities inheriting BaseTaskEntity (SprintTasks, WorkflowTasks, ActivityTasks, SubTasks, Risks, Assumptions, Issues, WorkDependencies).
/// 3. CalendarEntity records.
/// Enforces write permissions according to scope level:
/// - Tenant level: Must be Tenant Admin / Root Admin.
/// - Program level: ProgramRole >= Lead (CanWrite).
/// - Project level: ProjectRole >= Contribute (CanWrite).
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
        EnforceHierarchyWritePermissions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await EnforceHierarchyWritePermissionsAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceHierarchyWritePermissions(DbContext? context)
    {
        if (context is null) return;

        var currentUser = (context as AppDbContext)?.CurrentUser ?? new DefaultCurrentUser();
        if (currentUser.IsSystemDaemon || currentUser.IsRootAdmin) return;

        var entries = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return;

        var hasSecuredEntity = entries.Any(e => e.Entity is ProjectScopedEntity or BaseTaskEntity or CalendarEntity);
        if (!hasSecuredEntity) return;

        if (!currentUser.IsAuthenticated)
        {
            _logger.LogCritical("SECURITY INTERCEPTOR VIOLATION: Unauthenticated user attempted to mutate secured scoped entities.");
            throw new UnauthorizedAccessException("Fail-Closed Security Interceptor: Unauthenticated write to scoped entity is forbidden.");
        }

        var userId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogCritical("SECURITY INTERCEPTOR VIOLATION: Authenticated user has empty UserId when mutating secured scoped entities.");
            throw new UnauthorizedAccessException("Fail-Closed Security Interceptor: Authenticated write requires a valid UserId.");
        }

        using var scope = _scopeFactory.CreateScope();
        InitializeTransientScope(scope, context);
        var authService = scope.ServiceProvider.GetService<IHierarchyAuthorizationService>();
        if (authService is null) return;

        foreach (var entry in entries)
        {
            if (entry.Entity is BaseTaskEntity taskEntity)
            {
                ResolveAndVerifyScopedItem(context, authService, userId, taskEntity.Level, taskEntity.TenantId, taskEntity.ProgramId, taskEntity.ProjectId, taskEntity);
            }
            else if (entry.Entity is CalendarEntity calendarEntity)
            {
                ResolveAndVerifyScopedItem(context, authService, userId, calendarEntity.Level, calendarEntity.TenantId, calendarEntity.ProgramId, calendarEntity.ProjectId, calendarEntity);
            }
            else if (entry.Entity is ProjectScopedEntity projectScoped && projectScoped.ProjectId.HasValue && projectScoped.ProjectId.Value != Guid.Empty)
            {
                var role = authService.GetEffectiveProjectRoleAsync(projectScoped.ProjectId.Value, userId).GetAwaiter().GetResult();
                if (role is null || !role.Value.CanWrite())
                {
                    _logger.LogCritical("SECURITY INTERCEPTOR VIOLATION: User '{UserId}' lacks write permissions on project '{ProjectId}'", userId, projectScoped.ProjectId.Value);
                    throw new UnauthorizedAccessException($"Fail-Closed Security Interceptor: User '{userId}' does not have write permissions for project '{projectScoped.ProjectId.Value}'.");
                }
            }
        }
    }

    private async Task EnforceHierarchyWritePermissionsAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null) return;

        var currentUser = (context as AppDbContext)?.CurrentUser ?? new DefaultCurrentUser();
        if (currentUser.IsSystemDaemon || currentUser.IsRootAdmin) return;

        var entries = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return;

        var hasSecuredEntity = entries.Any(e => e.Entity is ProjectScopedEntity or BaseTaskEntity or CalendarEntity);
        if (!hasSecuredEntity) return;

        if (!currentUser.IsAuthenticated)
        {
            _logger.LogCritical("SECURITY INTERCEPTOR VIOLATION: Unauthenticated user attempted to mutate secured scoped entities.");
            throw new UnauthorizedAccessException("Fail-Closed Security Interceptor: Unauthenticated write to scoped entity is forbidden.");
        }

        var userId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogCritical("SECURITY INTERCEPTOR VIOLATION: Authenticated user has empty UserId when mutating secured scoped entities.");
            throw new UnauthorizedAccessException("Fail-Closed Security Interceptor: Authenticated write requires a valid UserId.");
        }

        using var scope = _scopeFactory.CreateScope();
        InitializeTransientScope(scope, context);
        var authService = scope.ServiceProvider.GetService<IHierarchyAuthorizationService>();
        if (authService is null) return;

        foreach (var entry in entries)
        {
            if (entry.Entity is BaseTaskEntity taskEntity)
            {
                await ResolveAndVerifyScopedItemAsync(context, authService, userId, taskEntity.Level, taskEntity.TenantId, taskEntity.ProgramId, taskEntity.ProjectId, taskEntity, ct);
            }
            else if (entry.Entity is CalendarEntity calendarEntity)
            {
                await ResolveAndVerifyScopedItemAsync(context, authService, userId, calendarEntity.Level, calendarEntity.TenantId, calendarEntity.ProgramId, calendarEntity.ProjectId, calendarEntity, ct);
            }
            else if (entry.Entity is ProjectScopedEntity projectScoped && projectScoped.ProjectId.HasValue && projectScoped.ProjectId.Value != Guid.Empty)
            {
                var role = await authService.GetEffectiveProjectRoleAsync(projectScoped.ProjectId.Value, userId, ct);
                if (role is null || !role.Value.CanWrite())
                {
                    _logger.LogCritical("SECURITY INTERCEPTOR VIOLATION: User '{UserId}' lacks write permissions on project '{ProjectId}'", userId, projectScoped.ProjectId.Value);
                    throw new UnauthorizedAccessException($"Fail-Closed Security Interceptor: User '{userId}' does not have write permissions for project '{projectScoped.ProjectId.Value}'.");
                }
            }
        }
    }

    private void ResolveAndVerifyScopedItem(DbContext context, IHierarchyAuthorizationService authService, string userId, TaskLevelEnum level, Guid tenantId, Guid? programId, Guid? projectId, object entity)
    {
        (programId, projectId) = ResolveRelationsIfMissing(context, entity, programId, projectId);
        authService.EnsureCanEditScopedItemAsync(level, tenantId, programId, projectId, userId).GetAwaiter().GetResult();
    }

    private async Task ResolveAndVerifyScopedItemAsync(DbContext context, IHierarchyAuthorizationService authService, string userId, TaskLevelEnum level, Guid tenantId, Guid? programId, Guid? projectId, object entity, CancellationToken ct)
    {
        (programId, projectId) = await ResolveRelationsIfMissingAsync(context, entity, programId, projectId, ct);
        await authService.EnsureCanEditScopedItemAsync(level, tenantId, programId, projectId, userId, ct);
    }

    private (Guid? ProgramId, Guid? ProjectId) ResolveRelationsIfMissing(DbContext context, object entity, Guid? programId, Guid? projectId)
    {
        if (programId.HasValue || projectId.HasValue) return (programId, projectId);

        if (entity is SubTaskEntity subTask && subTask.ParentTaskId != Guid.Empty)
        {
            var parentTask = context.Set<TaskEntity>().AsNoTracking().FirstOrDefault(t => t.Id == subTask.ParentTaskId);
            if (parentTask != null) return (parentTask.ProgramId, parentTask.ProjectId);
        }

        return (programId, projectId);
    }

    private async Task<(Guid? ProgramId, Guid? ProjectId)> ResolveRelationsIfMissingAsync(DbContext context, object entity, Guid? programId, Guid? projectId, CancellationToken ct)
    {
        if (programId.HasValue || projectId.HasValue) return (programId, projectId);

        if (entity is SubTaskEntity subTask && subTask.ParentTaskId != Guid.Empty)
        {
            var parentTask = await context.Set<TaskEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == subTask.ParentTaskId, ct);
            if (parentTask != null) return (parentTask.ProgramId, parentTask.ProjectId);
        }

        return (programId, projectId);
    }

    private void InitializeTransientScope(IServiceScope scope, DbContext context)
    {
        if (context is AppDbContext appDb)
        {
            var scopedTenant = scope.ServiceProvider.GetService<ITenantContext>();
            if (scopedTenant != null && appDb.TenantContext.HasActiveTenant)
            {
                scopedTenant.Initialize(
                    appDb.TenantContext.TenantId,
                    appDb.TenantContext.TenantName,
                    appDb.TenantContext.UserType,
                    appDb.TenantContext.AllowedTenants,
                    appDb.TenantContext.TenantSlug,
                    appDb.TenantContext.IsRootTenant);
            }

            var scopedUser = scope.ServiceProvider.GetService<ICurrentUser>();
            if (scopedUser != null && appDb.CurrentUser.IsAuthenticated)
            {
                scopedUser.RestoreUserContext(
                    appDb.CurrentUser.UserId,
                    appDb.CurrentUser.Email,
                    appDb.CurrentUser.UserName,
                    appDb.CurrentUser.IsRootAdmin);
            }
        }
    }
}