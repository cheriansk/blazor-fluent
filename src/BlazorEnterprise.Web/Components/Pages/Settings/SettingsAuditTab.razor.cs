using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Auditing;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Shared.Contracts.Common;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Settings;

public partial class SettingsAuditTab : ComponentBase, IDisposable
{
    [Inject] private IAuditService AuditService { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private IRootAdminService RootAdmin { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private IUserTimeZoneService TimeZoneService { get; set; } = default!;

    public record TenantFilterOption(string Id, string Name);
    public record UserFilterOption(string Id, string DisplayName);

    private List<TenantFilterOption> _availableTenants = new();
    private string? _selectedTenantId;

    private List<UserFilterOption> _availableUsers = new();
    private string? _selectedUserId;

    private AuditTrailFilterReqDto _filter = new() { PageSize = 15 };
    private PagedResultRespDto<AuditRecordEntity>? _auditResult;
    private bool _isLoading = true;
    private AuditRecordEntity? _selectedAuditRecord;
    private string _inGridSearchText = string.Empty;

    private readonly List<string> _eventTypes = ["All", "EntityChange", "Security", "TenantSwitch", "UserActivity"];
    private string _selectedEventType = "All";

    private readonly List<string> _severities = ["All", "Information", "Warning", "Error", "Critical"];
    private string _selectedSeverity = "All";

    private IQueryable<AuditRecordEntity> FilteredAuditItems
    {
        get
        {
            var items = _auditResult?.Items ?? [];
            var query = items.AsQueryable();
            if (!string.IsNullOrWhiteSpace(_inGridSearchText))
            {
                var term = _inGridSearchText.Trim();
                query = query.Where(a =>
                    (a.Description != null && a.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    a.TenantId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (a.UserId != null && a.UserId.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (a.UserEmail != null && a.UserEmail.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    a.EventType.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            return query;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        TimeZoneService.OnChange += HandleTimeZoneChanged;
        await LoadAvailableTenantsAsync();
        await LoadAvailableUsersAsync();
        await LoadAuditsAsync();
    }

    private async Task LoadAvailableTenantsAsync()
    {
        var isRootAdmin = CurrentUser.IsRootAdmin;
        if (isRootAdmin)
        {
            var dbTenants = (await TenantService.GetAllTenantsAsync())
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(
                    t.Slug,
                    t.Slug == RootAdmin.RootTenantSlug ? $"{t.Name} (Default Root)" : t.Name))
                .ToList();

            _availableTenants = dbTenants;
        }
        else
        {
            _availableTenants = TenantContext.AllowedTenants
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new TenantFilterOption(t.Id.ToString(), t.Name))
                .ToList();
        }

        if (_availableTenants.Count == 1)
        {
            await OnTenantSelectedAsync(_availableTenants[0].Id);
        }
    }

    private async Task LoadAvailableUsersAsync()
    {
        var users = new List<UserFilterOption>();

        if (CurrentUser.IsRootAdmin)
        {
            users.Add(new UserFilterOption("system", "⚙️ System / Daemon User"));
        }

        try
        {
            IEnumerable<UserFilterOption> dbUsers;
            if (!string.IsNullOrWhiteSpace(_selectedTenantId))
            {
                var tenantGuid = Guid.TryParse(_selectedTenantId, out var g) ? g : Guid.Empty;
                var tenantUsers = await TenantService.GetTenantUsersByIdAsync(tenantGuid);
                dbUsers = tenantUsers
                    .OrderBy(u => u.FullName)
                    .Take(100)
                    .Select(u => new UserFilterOption(u.Id.ToString(), $"{u.FullName} ({u.Email})"));
            }
            else if (CurrentUser.IsRootAdmin)
            {
                var allUsers = await TenantService.GetAllUsersAsync(100);
                dbUsers = allUsers
                    .Select(u => new UserFilterOption(u.Id.ToString(), $"{u.FullName} ({u.Email})"));
            }
            else
            {
                var tenantUsers = await TenantService.GetTenantUsersAsync(TenantContext.TenantSlug);
                dbUsers = tenantUsers
                    .OrderBy(u => u.FullName)
                    .Take(100)
                    .Select(u => new UserFilterOption(u.Id.ToString(), $"{u.FullName} ({u.Email})"));
            }

            users.AddRange(dbUsers);
        }
        catch
        {
            // Fallback gracefully if users cannot be queried
        }

        _availableUsers = users;
    }

    private async Task OnTenantSelectedAsync(string? tenantId)
    {
        _selectedTenantId = tenantId;
        _filter.TenantId = tenantId;
        _filter.PageNumber = 1;
        await LoadAvailableUsersAsync();
        await LoadAuditsAsync();
    }

    private async Task OnUserSelected(string? userId)
    {
        _selectedUserId = userId;
        _filter.UserId = userId;
        _filter.PageNumber = 1;
        await LoadAuditsAsync();
    }

    private async Task LoadAuditsAsync()
    {
        _isLoading = true;
        try
        {
            if (!CurrentUser.IsRootAdmin && string.IsNullOrWhiteSpace(_filter.TenantId))
            {
                _filter.AllowedTenantIds = TenantContext.AllowedTenants.Select(t => t.Id.ToString()).ToList();
            }
            else
            {
                _filter.AllowedTenantIds = null;
            }

            _auditResult = await AuditService.GetAuditTrailAsync(_filter);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnEventTypeChanged(string val)
    {
        _selectedEventType = val;
        _filter.EventType = val switch
        {
            "EntityChange" => AuditEventTypeEnum.EntityChange,
            "Security" => AuditEventTypeEnum.Security,
            "TenantSwitch" => AuditEventTypeEnum.TenantSwitch,
            "UserActivity" => AuditEventTypeEnum.UserActivity,
            _ => null
        };
    }

    private void OnSeverityChanged(string val)
    {
        _selectedSeverity = val;
        _filter.Severity = val switch
        {
            "Information" => AuditSeverityEnum.Information,
            "Warning" => AuditSeverityEnum.Warning,
            "Error" => AuditSeverityEnum.Error,
            "Critical" => AuditSeverityEnum.Critical,
            _ => null
        };
    }

    private async Task SearchAsync()
    {
        _filter.PageNumber = 1;
        await LoadAuditsAsync();
    }

    private async Task ResetFiltersAsync()
    {
        _selectedTenantId = null;
        _selectedUserId = null;
        _filter = new AuditTrailFilterReqDto { PageSize = 15 };
        _selectedEventType = "All";
        _selectedSeverity = "All";
        await LoadAvailableUsersAsync();
        await LoadAuditsAsync();
    }

    private async Task PreviousPageAsync()
    {
        if (_auditResult?.HasPreviousPage == true)
        {
            _filter.PageNumber--;
            await LoadAuditsAsync();
        }
    }

    private async Task NextPageAsync()
    {
        if (_auditResult?.HasNextPage == true)
        {
            _filter.PageNumber++;
            await LoadAuditsAsync();
        }
    }

    private void HandleTimeZoneChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TimeZoneService.OnChange -= HandleTimeZoneChanged;
    }
}
