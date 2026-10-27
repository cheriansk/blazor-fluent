using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Modules.KnowledgeBase.Pages;

public partial class KnowledgeArticlesListPage : ComponentBase
{
    [Inject] private IKnowledgeBaseService KnowledgeService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;

private bool _isLoading = true;
    private string _searchTerm = string.Empty;
    private KnowledgeArticleStatusEnum? _selectedStatus = null;
    private bool? _isGlobalFilter = null;
    private string _selectedLabel = "All Labels";

    private List<KnowledgeArticleListDto> _articles = new();
    private List<string> _availableLabels = new();
    private List<string> _labelSelectItems = new();

    private bool IsInternalUser =>
        CurrentUser.IsAuthenticated && (CurrentUser.IsRootAdmin || TenantContext.UserType == UserTypeEnum.CompanyUser);

    protected override async Task OnInitializedAsync()
    {
        await LoadLabelsAsync();
        await LoadArticlesAsync();
    }

    private async Task LoadLabelsAsync()
    {
        try
        {
            _availableLabels = await KnowledgeService.GetAllLabelsAsync();
            _labelSelectItems = new List<string> { "All Labels" };
            _labelSelectItems.AddRange(_availableLabels);
        }
        catch
        {
            _availableLabels = new();
            _labelSelectItems = new List<string> { "All Labels" };
        }
    }

    private async Task LoadArticlesAsync()
    {
        _isLoading = true;
        try
        {
            var filter = string.IsNullOrWhiteSpace(_selectedLabel) || _selectedLabel == "All Labels"
                ? null
                : _selectedLabel;
            _articles = await KnowledgeService.GetArticlesAsync(_searchTerm, _selectedStatus, filter, _isGlobalFilter);
        }
        catch (Exception)
        {
            _articles = new();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SetStatusFilter(KnowledgeArticleStatusEnum? status)
    {
        _selectedStatus = status;
        await LoadArticlesAsync();
    }

    private async Task SetScopeFilter(bool? isGlobal)
    {
        _isGlobalFilter = isGlobal;
        await LoadArticlesAsync();
    }
}