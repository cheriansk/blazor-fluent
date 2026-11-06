using Microsoft.AspNetCore.Components;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Models;

namespace BlazorEnterprise.Web.Components.Common;

public partial class RevisionToolbarComp : ComponentBase
{
    [Parameter] public TaskLevelEnum ScopeLevel { get; set; }
    [Parameter] public VersionedFeatureTypeEnum FeatureType { get; set; }
    [Parameter] public Guid? ProgramId { get; set; }
    [Parameter] public Guid? ProjectId { get; set; }
    [Parameter] public EventCallback<Guid> OnRevisionSelected { get; set; }

    protected List<RevisionSummaryDto> Revisions { get; set; } = new();
    protected Guid? SelectedRevisionId { get; set; }
    protected RevisionSummaryDto? CurrentRevision => Revisions.FirstOrDefault(r => r.Id == SelectedRevisionId);
    protected bool IsLoading { get; set; }
    protected bool IsMultiDraftMode => VersionedFeatureTypeEnumDefinitions.GetVersioningMode(FeatureType) == VersioningModeEnum.MultiDraft;

    // Modals
    protected bool ShowNewDraftModal { get; set; }
    protected string NewDraftName { get; set; } = string.Empty;
    protected string SourceRevisionIdForClone { get; set; } = string.Empty;

    protected bool ShowSubmitModal { get; set; }
    protected string ReviewerEmails { get; set; } = string.Empty;
    protected string ReviewNotes { get; set; } = string.Empty;

    protected bool ShowRejectModal { get; set; }
    protected string RejectReason { get; set; } = string.Empty;

    protected bool ShowCompareModal { get; set; }
    protected RevisionComparisonDto? ComparisonResult { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadRevisionsAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadRevisionsAsync();
    }

    private async Task LoadRevisionsAsync()
    {
        IsLoading = true;
        try
        {
            Revisions = await RevisionService.GetRevisionsAsync(ScopeLevel, FeatureType, ProgramId, ProjectId);
            if (SelectedRevisionId == null || !Revisions.Any(r => r.Id == SelectedRevisionId))
            {
                var active = Revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Active);
                SelectedRevisionId = active?.Id ?? Revisions.FirstOrDefault()?.Id;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task OnRevisionChangedAsync(ChangeEventArgs e)
    {
        if (Guid.TryParse(e.Value?.ToString(), out var id))
        {
            SelectedRevisionId = id;
            if (OnRevisionSelected.HasDelegate)
            {
                await OnRevisionSelected.InvokeAsync(id);
            }
        }
    }

    protected void OpenNewDraftModal()
    {
        NewDraftName = string.Empty;
        SourceRevisionIdForClone = string.Empty;
        ShowNewDraftModal = true;
    }

    protected async Task CreateDraftAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDraftName)) return;

        IsLoading = true;
        try
        {
            Guid? sourceId = Guid.TryParse(SourceRevisionIdForClone, out var parsed) ? parsed : null;
            var draft = await RevisionService.CreateDraftAsync(
                ScopeLevel, FeatureType, NewDraftName, sourceId, ProgramId, ProjectId);

            ShowNewDraftModal = false;
            await LoadRevisionsAsync();
            SelectedRevisionId = draft.Id;
            if (OnRevisionSelected.HasDelegate)
            {
                await OnRevisionSelected.InvokeAsync(draft.Id);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void OpenSubmitModal()
    {
        ReviewerEmails = string.Empty;
        ReviewNotes = string.Empty;
        ShowSubmitModal = true;
    }

    protected async Task SubmitForReviewAsync()
    {
        if (SelectedRevisionId == null || string.IsNullOrWhiteSpace(ReviewerEmails)) return;

        IsLoading = true;
        try
        {
            await RevisionService.SubmitForReviewAsync(SelectedRevisionId.Value, ReviewerEmails, ReviewNotes);
            ShowSubmitModal = false;
            await LoadRevisionsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task ApproveRevisionAsync()
    {
        if (SelectedRevisionId == null) return;

        IsLoading = true;
        try
        {
            await RevisionService.ApproveRevisionAsync(SelectedRevisionId.Value);
            await LoadRevisionsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void OpenRejectModal()
    {
        RejectReason = string.Empty;
        ShowRejectModal = true;
    }

    protected async Task RejectRevisionAsync()
    {
        if (SelectedRevisionId == null || string.IsNullOrWhiteSpace(RejectReason)) return;

        IsLoading = true;
        try
        {
            await RevisionService.RejectRevisionAsync(SelectedRevisionId.Value, RejectReason);
            ShowRejectModal = false;
            await LoadRevisionsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task ActivateRevisionAsync()
    {
        if (SelectedRevisionId == null) return;

        IsLoading = true;
        try
        {
            await RevisionService.ActivateRevisionAsync(SelectedRevisionId.Value);
            await LoadRevisionsAsync();
            if (OnRevisionSelected.HasDelegate)
            {
                await OnRevisionSelected.InvokeAsync(SelectedRevisionId.Value);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task DiscardDraftAsync()
    {
        if (SelectedRevisionId == null) return;

        IsLoading = true;
        try
        {
            await RevisionService.DiscardDraftAsync(SelectedRevisionId.Value);
            await LoadRevisionsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task OpenCompareModalAsync()
    {
        if (SelectedRevisionId == null) return;

        var active = Revisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Active);
        var targetId = active?.Id ?? Revisions.FirstOrDefault(r => r.Id != SelectedRevisionId)?.Id;
        if (targetId == null || targetId == SelectedRevisionId) return;

        IsLoading = true;
        try
        {
            ComparisonResult = await RevisionService.CompareRevisionsAsync(targetId.Value, SelectedRevisionId.Value);
            ShowCompareModal = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task FinalizeBaselineAsync()
    {
        if (SelectedRevisionId == null) return;
        IsLoading = true;
        try
        {
            var result = await RevisionService.FinalizeAsync(SelectedRevisionId.Value);
            await LoadRevisionsAsync();
            SelectedRevisionId = result.WorkingDraft.Id;
            if (OnRevisionSelected.HasDelegate)
            {
                await OnRevisionSelected.InvokeAsync(result.WorkingDraft.Id);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task CloseCycleAsync()
    {
        if (SelectedRevisionId == null) return;
        IsLoading = true;
        try
        {
            var closed = await RevisionService.CloseCycleAsync(SelectedRevisionId.Value);
            await LoadRevisionsAsync();
            SelectedRevisionId = closed.Id;
            if (OnRevisionSelected.HasDelegate)
            {
                await OnRevisionSelected.InvokeAsync(closed.Id);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
