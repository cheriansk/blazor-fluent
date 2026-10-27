using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Infrastructure.Security;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Components.Layout;

public partial class TenantAndProjectSwitcherComp : ComponentBase
{
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private IAuditService AuditService { get; set; } = default!;
    [Inject] private ProtectedSessionStorage SessionStorage { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IUnsavedChangesStateService UnsavedChanges { get; set; } = default!;
    [Inject] private ILogger<TenantAndProjectSwitcherComp> Logger { get; set; } = default!;

    private List<ProjectInfoDto> _projects = new();
    private bool _isLoadingProjects;
    private string? _switchError;

    protected override async Task OnInitializedAsync()
    {
        await LoadProjectsForCurrentTenantAsync();
    }

    private async Task LoadProjectsForCurrentTenantAsync()
    {
        if (!TenantContext.HasActiveTenant || TenantContext.TenantId == Guid.Empty || TenantContext.IsRootTenant) return;

        try
        {
            _isLoadingProjects = true;
            var entities = await TenantService.GetTenantProjectsAsync(TenantContext.TenantSlug);
            _projects = entities
                .Select(e => new ProjectInfoDto(e.Id, e.Name, e.ShortCode, e.Status))
                .ToList();

            ProjectContext.Initialize(
                ProjectContext.ProjectId,
                ProjectContext.ProjectName,
                ProjectContext.ProjectShortCode,
                _projects);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load projects for tenant '{TenantSlug}'", TenantContext.TenantSlug);
        }
        finally
        {
            _isLoadingProjects = false;
        }
    }

    private async Task OnTenantChanged(string? newTenantId)
    {
        if (string.IsNullOrWhiteSpace(newTenantId) || (Guid.TryParse(newTenantId, out var gId) && gId == TenantContext.TenantId)) return;

        var prevTenant = TenantContext.TenantSlug;

        var targetMatch = TenantContext.AllowedTenants.FirstOrDefault(t => t.Id.ToString() == newTenantId || t.Slug == newTenantId);
        if (targetMatch == null)
        {
            _switchError = "You do not have permission to access the requested tenant.";
            await AuditService.LogSecurityEventAsync(
                "TenantSwitchBlocked",
                AuditSeverityEnum.Critical,
                $"Attempted unauthorized switch from '{prevTenant}' to '{newTenantId}'.");
            return;
        }

        _switchError = null;

        await AuditService.LogSecurityEventAsync(
            "TenantSwitchInitiated",
            AuditSeverityEnum.Information,
            $"User initiated tenant switch from '{prevTenant}' to '{targetMatch.Slug}'. Purging session and enforcing re-authentication.");

        try
        {
            await SessionStorage.DeleteAsync("app_session");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to purge session storage during tenant switch.");
        }

        ProjectContext.Reset();
        TenantContext.Reset();
        UnsavedChanges.ResetAll();

        NavigationManager.NavigateTo($"/login?tenant={Uri.EscapeDataString(targetMatch.Slug)}", forceLoad: true);
    }

    private async Task OnProjectChanged(string? newProjectIdStr)
    {
        if (string.IsNullOrWhiteSpace(newProjectIdStr)) return;
        if (!Guid.TryParse(newProjectIdStr, out var newProjectId)) return;
        if (ProjectContext.ProjectId == newProjectId) return;

        var result = ProjectContext.SetActiveProject(newProjectId);
        if (!result.Succeeded)
        {
            _switchError = result.Errors.FirstOrDefault() ?? "Project switch failed.";
            return;
        }

        _switchError = null;

        await AuditService.LogUserActivityAsync(
            "ProjectWorkspaceSwitched",
            $"User switched active project workspace to '{ProjectContext.ProjectName}' ({newProjectId}).");

        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }
}
