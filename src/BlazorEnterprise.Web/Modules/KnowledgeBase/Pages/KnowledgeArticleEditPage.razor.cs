using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Validation;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Modules.KnowledgeBase.Pages;

public partial class KnowledgeArticleEditPage : ComponentBase
{
    [Inject] private IKnowledgeBaseService KnowledgeService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

[Parameter] public Guid? Id { get; set; }

    private bool IsEditMode => Id.HasValue && Id.Value != Guid.Empty;

    private bool _isLoading = true;
    private bool _isSubmitting = false;
    private string? _statusMessage;
    private bool _isStatusError = false;

    private CreateArticleDto _model = new();
    private List<UserDto> _availableReviewers = new();

    // Validation state flags
    private bool _scopeError = false;
    private bool _labelsError = false;
    private bool _reviewersError = false;
    private bool _contentError = false;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _availableReviewers = await KnowledgeService.GetAvailableReviewersAsync();

            if (IsEditMode)
            {
                var existing = await KnowledgeService.GetArticleByIdAsync(Id!.Value);
                if (existing != null)
                {
                    _model = new CreateArticleDto
                    {
                        Title = existing.Title,
                        Content = existing.Content,
                        Scope = existing.IsGlobal ? KnowledgeArticleScopeEnum.Global : KnowledgeArticleScopeEnum.TenantOnly,
                        Labels = existing.Labels.ToList(),
                        ProjectId = existing.ProjectId,
                        ReviewerUserIds = existing.Reviewers.Select(r => r.UserId).ToList()
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error loading article form: {ex.Message}";
            _isStatusError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleReviewer(string userId, bool isSelected)
    {
        if (isSelected && !_model.ReviewerUserIds.Contains(userId))
        {
            _model.ReviewerUserIds.Add(userId);
        }
        else if (!isSelected && _model.ReviewerUserIds.Contains(userId))
        {
            _model.ReviewerUserIds.Remove(userId);
        }
    }

    private bool ValidateForm()
    {
        _scopeError = _model.Scope == null;
        _labelsError = _model.Labels == null || _model.Labels.Count < 1 || _model.Labels.Count > 10;
        _reviewersError = _model.ReviewerUserIds == null || _model.ReviewerUserIds.Count < 1;
        _contentError = string.IsNullOrWhiteSpace(_model.Content) || _model.Content.Length > 20000;

        if (string.IsNullOrWhiteSpace(_model.Title) || _model.Title.Length > 100)
        {
            _statusMessage = "Please provide a valid title (1-100 characters).";
            _isStatusError = true;
            return false;
        }

        if (_scopeError || _labelsError || _reviewersError || _contentError)
        {
            _statusMessage = "Please resolve the required fields before submitting.";
            _isStatusError = true;
            return false;
        }

        _statusMessage = null;
        return true;
    }

    private async Task SaveArticleAsync()
    {
        if (!ValidateForm()) return;

        _isSubmitting = true;
        try
        {
            if (IsEditMode)
            {
                await KnowledgeService.UpdateArticleAsync(new UpdateArticleDto
                {
                    Id = Id!.Value,
                    Title = _model.Title,
                    Content = _model.Content,
                    Scope = _model.Scope,
                    Labels = _model.Labels,
                    ProjectId = _model.ProjectId,
                    ReviewerUserIds = _model.ReviewerUserIds
                });

                NavigationManager.NavigateTo($"/knowledge/{Id.Value}");
            }
            else
            {
                var newId = await KnowledgeService.CreateArticleAsync(_model);
                NavigationManager.NavigateTo($"/knowledge/{newId}");
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error saving article: {ex.Message}";
            _isStatusError = true;
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}