using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Modules.KnowledgeBase.Pages;

public partial class KnowledgeArticleViewPage : ComponentBase
{
    [Inject] private IKnowledgeBaseService KnowledgeService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;

[Parameter] public Guid Id { get; set; }

    private bool _isLoading = true;
    private bool _isApproving = false;
    private string? _statusMessage;
    private bool _isStatusError = false;
    private KnowledgeArticleDetailDto? _article;

    private bool CanApprove =>
        _article != null &&
        _article.Status == KnowledgeArticleStatusEnum.PendingReview &&
        (_article.Reviewers.Any(r => r.UserId == CurrentUser.UserId && !r.HasApproved) ||
         CurrentUser.IsRootAdmin);

    protected override async Task OnInitializedAsync()
    {
        await LoadArticleAsync();
    }

    private async Task LoadArticleAsync()
    {
        _isLoading = true;
        try
        {
            _article = await KnowledgeService.GetArticleByIdAsync(Id);
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error loading article: {ex.Message}";
            _isStatusError = true;
            _article = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ApproveArticleAsync()
    {
        _isApproving = true;
        try
        {
            var success = await KnowledgeService.ApproveArticleAsync(Id);
            if (success)
            {
                _statusMessage = "Article approved successfully!";
                _isStatusError = false;
                await LoadArticleAsync();
            }
            else
            {
                _statusMessage = "Failed to approve article. Ensure you have reviewer permissions.";
                _isStatusError = true;
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error during approval: {ex.Message}";
            _isStatusError = true;
        }
        finally
        {
            _isApproving = false;
        }
    }
}