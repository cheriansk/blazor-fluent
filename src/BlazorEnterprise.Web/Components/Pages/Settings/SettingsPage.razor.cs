using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsPage : ComponentBase
{
    [Inject] private ISettingsNavigationState SettingsNav { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IHierarchyAuthorizationService HierarchyAuth { get; set; } = default!;

    private string _activeTabId = "projects";
    private readonly HashSet<string> _visitedTabs = new(StringComparer.OrdinalIgnoreCase);
    private bool _canViewTenantTab;
    private bool _canViewProgramTab;

    private bool CanViewTenantTab => CurrentUser.IsRootAdmin || _canViewTenantTab;
    private bool CanViewProgramTab => CurrentUser.IsRootAdmin || _canViewProgramTab;
    private bool CanViewAuditTrail =>
        CurrentUser.IsRootAdmin ||
        CurrentUser.IsInRole("Admin") ||
        CurrentUser.IsInRole("CompanyAdmin");

    protected override async Task OnInitializedAsync()
    {
        if (CurrentUser.IsRootAdmin)
        {
            _canViewTenantTab = true;
            _canViewProgramTab = true;
        }
        else
        {
            if (TenantContext.HasActiveTenant && CurrentUser.IsAuthenticated)
            {
                var tenantId = TenantContext.TenantId;
                var userId = CurrentUser.UserId;
                _canViewTenantTab = await HierarchyAuth.CanViewTenantSettingsAsync(tenantId, userId);
                var accessiblePrograms = await HierarchyAuth.GetAccessibleProgramIdsAsync(tenantId, userId);
                _canViewProgramTab = _canViewTenantTab || accessiblePrograms.Count > 0;
            }
        }

        _activeTabId = CanViewTenantTab ? "tenants" : (CanViewProgramTab ? "programs" : "projects");

        if (!string.IsNullOrWhiteSpace(SettingsNav.InitialTab))
        {
            _activeTabId = SettingsNav.InitialTab;
            SettingsNav.InitialTab = null;
        }

        if (_activeTabId.Equals("tenants", StringComparison.OrdinalIgnoreCase) && !CanViewTenantTab)
        {
            _activeTabId = CanViewProgramTab ? "programs" : "projects";
        }

        if (_activeTabId.Equals("programs", StringComparison.OrdinalIgnoreCase) && !CanViewProgramTab)
        {
            _activeTabId = "projects";
        }

        if (_activeTabId.Equals("sessions", StringComparison.OrdinalIgnoreCase) && !CurrentUser.IsRootAdmin)
        {
            _activeTabId = "projects";
        }

        if (_activeTabId.Equals("auditing", StringComparison.OrdinalIgnoreCase) && !CanViewAuditTrail)
        {
            _activeTabId = "projects";
        }

        _visitedTabs.Add(_activeTabId);
    }

    private void SelectTab(string tabId)
    {
        _activeTabId = tabId;
        _visitedTabs.Add(tabId);
    }
}
