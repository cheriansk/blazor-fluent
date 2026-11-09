using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Resources;

public partial class ResourceSelectionModal : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public List<UserEntity> AvailableUsers { get; set; } = new();
    [Parameter] public EventCallback<List<(Guid UserId, ProjectResourceRoleEnum Role)>> OnConfirm { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private string _searchTerm = string.Empty;
    private ProjectResourceRoleEnum _defaultRole = ProjectResourceRoleEnum.Developer;
    private readonly HashSet<Guid> _selectedUserIds = new();

    private IEnumerable<UserEntity> FilteredUsers => AvailableUsers
        .Where(u => string.IsNullOrWhiteSpace(_searchTerm) ||
                    (u.FullName?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (u.Email?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));

    private void ToggleUserSelection(Guid userId)
    {
        if (_selectedUserIds.Contains(userId))
            _selectedUserIds.Remove(userId);
        else
            _selectedUserIds.Add(userId);
    }

    private async Task ConfirmSelection()
    {
        var result = _selectedUserIds.Select(id => (id, _defaultRole)).ToList();
        _selectedUserIds.Clear();
        await OnConfirm.InvokeAsync(result);
    }

    private async Task Cancel()
    {
        _selectedUserIds.Clear();
        await OnCancel.InvokeAsync();
    }
}
