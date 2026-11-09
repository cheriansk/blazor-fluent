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
    [Parameter] public EventCallback<Guid> OnRevisionChanged { get; set; }
    [Parameter] public EventCallback OnDataMutated { get; set; }

    [Parameter] public List<RevisionSummaryDto>? Revisions { get; set; }
    [Parameter] public Guid? SelectedRevisionId { get; set; }

    protected List<RevisionSummaryDto> EffectiveRevisions => Revisions ?? _internalRevisions;
    private List<RevisionSummaryDto> _internalRevisions = new();
    protected RevisionSummaryDto? CurrentRevision => EffectiveRevisions.FirstOrDefault(r => r.Id == SelectedRevisionId);
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
            if (Revisions == null)
            {
                _internalRevisions = await RevisionService.GetRevisionsAsync(ScopeLevel, FeatureType, ProgramId, ProjectId);
            }
            if (SelectedRevisionId == null || !EffectiveRevisions.Any(r => r.Id == SelectedRevisionId))
            {
                var active = EffectiveRevisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Active);
                SelectedRevisionId = active?.Id ?? EffectiveRevisions.FirstOrDefault()?.Id;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task NotifyRevisionChangedAsync(Guid id)
    {
        SelectedRevisionId = id;
        if (OnRevisionSelected.HasDelegate)
        {
            await OnRevisionSelected.InvokeAsync(id);
        }
        if (OnRevisionChanged.HasDelegate)
        {
            await OnRevisionChanged.InvokeAsync(id);
        }
    }

    private async Task NotifyDataMutatedAsync()
    {
        if (OnDataMutated.HasDelegate)
        {
            await OnDataMutated.InvokeAsync();
        }
    }

    protected async Task OnRevisionChangedAsync(ChangeEventArgs e)
    {
        if (Guid.TryParse(e.Value?.ToString(), out var id))
        {
            await NotifyRevisionChangedAsync(id);
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
            await NotifyRevisionChangedAsync(draft.Id);
            await NotifyDataMutatedAsync();
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
            await NotifyDataMutatedAsync();
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
            await NotifyDataMutatedAsync();
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
            await NotifyDataMutatedAsync();
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
            await NotifyRevisionChangedAsync(SelectedRevisionId.Value);
            await NotifyDataMutatedAsync();
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
            await NotifyDataMutatedAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task OpenCompareModalAsync()
    {
        if (SelectedRevisionId == null) return;

        var active = EffectiveRevisions.FirstOrDefault(r => r.Status == RevisionStatusEnum.Active);
        var targetId = active?.Id ?? EffectiveRevisions.FirstOrDefault(r => r.Id != SelectedRevisionId)?.Id;
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
            await NotifyRevisionChangedAsync(result.WorkingDraft.Id);
            await NotifyDataMutatedAsync();
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
            await NotifyRevisionChangedAsync(closed.Id);
            await NotifyDataMutatedAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
