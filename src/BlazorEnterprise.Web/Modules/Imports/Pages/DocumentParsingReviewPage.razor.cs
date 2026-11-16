using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Web.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Modules.Imports.Pages;

/// <summary>
/// Code-behind for <see cref="DocumentParsingReviewPage"/> managing tabular review,
/// JSON correction, lifecycle transitions, and permanent finalization.
/// </summary>
public partial class DocumentParsingReviewPage
{
    [Inject] private IDocumentParsingReviewService ReviewService { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private ICorrelationContext CorrelationContext { get; set; } = default!;
    [Inject] private ILogger<DocumentParsingReviewPage> PageLogger { get; set; } = default!;

    private List<DocumentParsingReviewItemDto> _items = new();
    private readonly HashSet<Guid> _selectedIds = new();
    private bool _isLoading = true;
    private bool _isBusy;
    private bool _isError;
    private string? _statusMessage;

    private CskDialog? _reviewDialog;
    private ConfirmDialogModal? _finalizeConfirmModal;
    private DocumentParsingReviewItemDto? _activeItem;
    private string _editableJson = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadResultsAsync();
    }

    private async Task LoadResultsAsync()
    {
        _isLoading = true;
        try
        {
            var projectId = ProjectContext.HasProject ? ProjectContext.ProjectId : null;
            var results = await ReviewService.GetResultsAsync(projectId);
            _items = results.ToList();
            _selectedIds.Clear();
        }
        catch (Exception ex)
        {
            PageLogger.LogError(ex, "Failed to load document parsing results. CorrelationId: {CorrelationId}", CorrelationContext.CorrelationId);
            _isError = true;
            _statusMessage = UserFacingError.For("load parsed documents", CorrelationContext.CorrelationId);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleSelection(Guid id, bool selected)
    {
        if (selected) _selectedIds.Add(id);
        else _selectedIds.Remove(id);
    }

    private async Task OpenReviewModal(DocumentParsingReviewItemDto item)
    {
        _activeItem = item;
        _editableJson = item.ExtractedDataJson;
        if (_reviewDialog is not null)
        {
            await _reviewDialog.ShowAsync();
        }
    }

    private async Task CloseReviewModalAsync()
    {
        if (_reviewDialog is not null)
        {
            await _reviewDialog.HideAsync();
        }
        _activeItem = null;
    }

    private async Task SaveEditsAsync()
    {
        if (_activeItem is null) return;
        _isBusy = true;
        try
        {
            var res = await ReviewService.UpdateExtractedDataAsync(_activeItem.Id, _editableJson);
            _isError = !res.IsSuccess;
            _statusMessage = res.IsSuccess ? "Corrections saved." : res.Error;
            if (res.IsSuccess)
            {
                await CloseReviewModalAsync();
                await LoadResultsAsync();
            }
        }
        catch (Exception ex)
        {
            PageLogger.LogError(ex, "Failed to save parsed document corrections. CorrelationId: {CorrelationId}", CorrelationContext.CorrelationId);
            _isError = true;
            _statusMessage = UserFacingError.For("save corrections", CorrelationContext.CorrelationId);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ChangeActiveStatusAsync(DocumentParsingStatusEnum newStatus)
    {
        if (_activeItem is null) return;
        _isBusy = true;
        try
        {
            var res = await ReviewService.ChangeStatusAsync(_activeItem.Id, newStatus);
            _isError = !res.IsSuccess;
            _statusMessage = res.IsSuccess ? $"Status updated to {newStatus}." : res.Error;
            if (res.IsSuccess)
            {
                await CloseReviewModalAsync();
                await LoadResultsAsync();
            }
        }
        catch (Exception ex)
        {
            PageLogger.LogError(ex, "Failed to change document parsing status. CorrelationId: {CorrelationId}", CorrelationContext.CorrelationId);
            _isError = true;
            _statusMessage = UserFacingError.For("update status", CorrelationContext.CorrelationId);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task PromptFinalizeSelectedAsync()
    {
        if (_selectedIds.Count == 0 || _finalizeConfirmModal is null) return;
        await _finalizeConfirmModal.ShowAsync();
    }

    private async Task ConfirmFinalizeAsync()
    {
        _isBusy = true;
        try
        {
            var res = await ReviewService.FinalizeBatchAsync(_selectedIds.ToList());
            _isError = !res.IsSuccess;
            _statusMessage = res.IsSuccess
                ? $"Permanently finalized {res.Value} document(s)."
                : res.Error;
            if (res.IsSuccess)
            {
                await LoadResultsAsync();
            }
        }
        catch (Exception ex)
        {
            PageLogger.LogError(ex, "Failed to finalize parsed documents. CorrelationId: {CorrelationId}", CorrelationContext.CorrelationId);
            _isError = true;
            _statusMessage = UserFacingError.For("finalize documents", CorrelationContext.CorrelationId);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private static CskStatusChip.ChipVariant GetStatusVariant(DocumentParsingStatusEnum status) => status switch
    {
        DocumentParsingStatusEnum.Finalized => CskStatusChip.ChipVariant.Success,
        DocumentParsingStatusEnum.ReviewComplete => CskStatusChip.ChipVariant.Info,
        _ => CskStatusChip.ChipVariant.Warning
    };
}
