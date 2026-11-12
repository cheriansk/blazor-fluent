using System.Collections.Generic;
using BlazorEnterprise.Core.Dtos.Tasks;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Dialogs;

public partial class TaskTypeSelectionButton : ComponentBase
{
    [Parameter] public string ButtonText { get; set; } = "New Task";
    [Parameter] public string? Icon { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public List<CreationTaskTypeEnum>? AllowedTypes { get; set; }
    [Parameter] public EventCallback<CreationTaskTypeEnum> OnTaskTypeSelected { get; set; }

    private bool _isOpen;

    private void ToggleDropdown()
    {
        if (!Disabled)
        {
            _isOpen = !_isOpen;
        }
    }

    private void CloseDropdown()
    {
        _isOpen = false;
    }

    private async Task SelectType(CreationTaskTypeEnum taskType)
    {
        _isOpen = false;
        await OnTaskTypeSelected.InvokeAsync(taskType);
    }

    private bool IsAllowed(CreationTaskTypeEnum type)
    {
        return AllowedTypes == null || AllowedTypes.Contains(type);
    }
}
