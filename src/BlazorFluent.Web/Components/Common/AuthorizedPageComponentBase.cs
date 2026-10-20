using System;
using System.Reflection;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.Attributes;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Components.Common;

/// <summary>
/// Mandatory base class for all feature components and pages that require project authorization.
/// Automatically resolves [RequiredClearance] attribute at runtime to evaluate CanVisit and CanEdit.
/// Enforces Zero-Trust active provisioning and emits Zero-PII security audit logs on denial.
/// </summary>
public abstract class AuthorizedPageComponentBase : ComponentBase
{
    [Inject] protected IProjectAuthorizationService ProjectAuth { get; set; } = default!;
    [Inject] protected IHierarchyAuthorizationService HierarchyAuth { get; set; } = default!;
    [Inject] protected ITenantContext TenantContext { get; set; } = default!;
    [Inject] protected ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected ILogger<AuthorizedPageComponentBase> Logger { get; set; } = default!;
    [Inject] protected IAuditService AuditService { get; set; } = default!;

    /// <summary>
    /// ID of the project being viewed or modified.
    /// Can be passed as a route parameter or component parameter.
    /// </summary>
    [Parameter] public Guid ProjectId { get; set; }

    /// <summary>
    /// Minimum role required to view/visit this page.
    /// Defaults dynamically from [RequiredClearance] attribute on derived page class.
    /// </summary>
    protected virtual ProjectRoleEnum MinimumVisitRole =>
        GetType().GetCustomAttribute<RequiredClearanceAttribute>()?.ViewTier ?? ProjectRoleEnum.ReadOnly;

    /// <summary>
    /// Minimum role required to edit or perform actions on this page.
    /// Defaults dynamically from [RequiredClearance] attribute on derived page class.
    /// </summary>
    protected virtual ProjectRoleEnum MinimumEditRole =>
        GetType().GetCustomAttribute<RequiredClearanceAttribute>()?.EditTier ?? ProjectRoleEnum.Contribute;

    /// <summary>
    /// True if the current user has permission to view this page.
    /// When false, the page should render the Access Denied card.
    /// </summary>
    protected bool CanVisit { get; private set; }

    /// <summary>
    /// True if the current user has permission to edit or submit mutations on this page.
    /// When false, the page renders in read-only mode (inputs disabled, save buttons hidden).
    /// </summary>
    protected bool CanEdit { get; private set; }

    /// <summary>
    /// Indicates whether permissions are currently being resolved from cache/database.
    /// </summary>
    protected bool IsEvaluating { get; private set; } = true;

    /// <summary>
    /// The user's active role within the specified project (null if no access).
    /// </summary>
    protected ProjectRoleEnum? CurrentRole { get; private set; }

    protected override async Task OnParametersSetAsync()
    {
        await EvaluatePermissionsAsync();
    }

    /// <summary>
    /// Evaluates CanVisit and CanEdit permissions against the active project context and account validity.
    /// </summary>
    protected virtual async Task EvaluatePermissionsAsync()
    {
        IsEvaluating = true;

        // Zero-Trust Perimeter Check: User must be active, unexpired, and hold >= 1 role
        if (!CurrentUser.IsRootAdmin && !string.IsNullOrWhiteSpace(CurrentUser.UserId))
        {
            var isProvisioned = await HierarchyAuth.IsUserActiveAndProvisionedAsync(CurrentUser.UserId);
            if (!isProvisioned)
            {
                Logger.LogWarning(
                    "AUDIT [Security]: Active circuit eviction. UserId='{UserId}' is inactive, expired, or unassigned.",
                    CurrentUser.UserId);

                NavigationManager.NavigateTo("/auth-error?reason=Account+is+inactive%2C+expired%2C+or+unassigned.", forceLoad: true);
                return;
            }
        }

        if (ProjectId == Guid.Empty)
        {
            // Multi-project overview or list page
            if (CurrentUser.IsRootAdmin)
            {
                CanVisit = true;
                CanEdit = true;
                CurrentRole = ProjectRoleEnum.Admin;
            }
            else
            {
                var authorizedIds = await ProjectAuth.GetAuthorizedProjectIdsAsync(MinimumVisitRole);
                CanVisit = authorizedIds.Count > 0;
                CanEdit = (await ProjectAuth.GetAuthorizedProjectIdsAsync(MinimumEditRole)).Count > 0;
            }
        }
        else
        {
            CurrentRole = await HierarchyAuth.GetEffectiveProjectRoleAsync(ProjectId);
            CanVisit = CurrentRole.HasValue && CurrentRole.Value.Satisfies(MinimumVisitRole);
            CanEdit = CurrentRole.HasValue && CurrentRole.Value.Satisfies(MinimumEditRole) && CurrentRole.Value.CanWrite();
        }

        if (!CanVisit && !CurrentUser.IsRootAdmin)
        {
            // Zero-PII warning logging (strictly GUIDs, zero personal email/name)
            Logger.LogWarning(
                "AUDIT [Security]: Page Access Denied. UserId='{UserId}', TenantId='{TenantId}', ProjectId='{ProjectId}', Page='{PageName}', RequiredTier='{RequiredTier}', ActualTier='{ActualTier}'",
                CurrentUser.UserId ?? "anonymous",
                TenantContext.TenantId ?? "none",
                ProjectId,
                GetType().Name,
                MinimumVisitRole,
                CurrentRole?.ToString() ?? "None");

            await AuditService.LogSecurityEventAsync(
                "PageAccessDenied",
                AuditSeverityEnum.Warning,
                $"UserId: {CurrentUser.UserId ?? "anonymous"}, ProjectId: {ProjectId}, Page: {GetType().Name}, RequiredTier: {MinimumVisitRole}, ActualTier: {CurrentRole?.ToString() ?? "None"}");
        }

        IsEvaluating = false;
        await OnPermissionsEvaluatedAsync();
    }

    /// <summary>
    /// Hook executed after permissions have been evaluated. Override in derived pages for custom setup.
    /// </summary>
    protected virtual Task OnPermissionsEvaluatedAsync() => Task.CompletedTask;
}
