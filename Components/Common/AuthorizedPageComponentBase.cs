using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorFluent.Components.Common;

/// <summary>
/// Mandatory base class for all feature components and pages that require project authorization.
/// Forces every inheriting page to declare its MinimumVisitRole and MinimumEditRole at compile-time.
/// Automatically evaluates CanVisit and CanEdit at runtime using cached project roles.
/// </summary>
public abstract class AuthorizedPageComponentBase : ComponentBase
{
    [Inject] protected IProjectAuthorizationService ProjectAuth { get; set; } = default!;
    [Inject] protected ITenantContext TenantContext { get; set; } = default!;
    [Inject] protected ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// ID of the project being viewed or modified.
    /// Can be passed as a route parameter or component parameter.
    /// </summary>
    [Parameter] public Guid ProjectId { get; set; }

    /// <summary>
    /// Minimum role required to view/visit this page (Compile-time contract).
    /// </summary>
    protected abstract ProjectRole MinimumVisitRole { get; }

    /// <summary>
    /// Minimum role required to edit or perform actions on this page (Compile-time contract).
    /// </summary>
    protected abstract ProjectRole MinimumEditRole { get; }

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
    protected ProjectRole? CurrentRole { get; private set; }

    protected override async Task OnParametersSetAsync()
    {
        await EvaluatePermissionsAsync();
    }

    /// <summary>
    /// Evaluates CanVisit and CanEdit permissions against the active project context.
    /// </summary>
    protected virtual async Task EvaluatePermissionsAsync()
    {
        IsEvaluating = true;

        if (ProjectId == Guid.Empty)
        {
            // If page is not tied to a specific project (e.g. multi-project overview),
            // check if user has access to at least one project or is host/admin.
            if (CurrentUser.IsRootAdmin)
            {
                CanVisit = true;
                CanEdit = true;
                CurrentRole = ProjectRole.Admin;
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
            CurrentRole = await ProjectAuth.GetCurrentUserProjectRoleAsync(ProjectId);
            CanVisit = CurrentRole.HasValue && CurrentRole.Value.Satisfies(MinimumVisitRole);
            CanEdit = CurrentRole.HasValue && CurrentRole.Value.Satisfies(MinimumEditRole) && CurrentRole.Value.CanWrite();
        }

        IsEvaluating = false;
        await OnPermissionsEvaluatedAsync();
    }

    /// <summary>
    /// Hook executed after permissions have been evaluated. Override in derived pages for custom setup.
    /// </summary>
    protected virtual Task OnPermissionsEvaluatedAsync() => Task.CompletedTask;
}
