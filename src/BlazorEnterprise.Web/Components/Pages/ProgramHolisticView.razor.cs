using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos.Tasks;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Web.Components.Pages.ProgramHolisticViewTabs;
using BlazorEnterprise.Web.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages;

public partial class ProgramHolisticView : ComponentBase, IDisposable
{
    private bool _isLoading = true;
    private int _loadId;
    private CancellationTokenSource? _loadCts;
    private ProgramHealthSummaryDto? _healthSummary;
    private IReadOnlyList<CalendarItemDto> _calendarEvents = Array.Empty<CalendarItemDto>();
    private IReadOnlyList<CalendarItemDto> _milestones = Array.Empty<CalendarItemDto>();
    private IReadOnlyList<RiskEntity> _risks = Array.Empty<RiskEntity>();
    private IReadOnlyList<AssumptionEntity> _assumptions = Array.Empty<AssumptionEntity>();
    private IReadOnlyList<IssueEntity> _issues = Array.Empty<IssueEntity>();
    private IReadOnlyList<WorkDependencyEntity> _blockers = Array.Empty<WorkDependencyEntity>();
    private List<TabItemModel> _tabs = new();

    private bool _isCreateDialogOpen;
    private CreationTaskTypeEnum _selectedCreationType = CreationTaskTypeEnum.Risk;
    private static readonly List<CreationTaskTypeEnum> _allowedProgramTaskTypes =
    [
        CreationTaskTypeEnum.MiscTask,
        CreationTaskTypeEnum.Risk,
        CreationTaskTypeEnum.Assumption,
        CreationTaskTypeEnum.Issue
    ];

    private void OpenCreateDialog(CreationTaskTypeEnum taskType)
    {
        _selectedCreationType = taskType;
        _isCreateDialogOpen = true;
    }

    private async Task HandleTaskCreated(TaskCreationRequestDto request)
    {
        await RefreshProgramDataAsync();
    }

    protected override async Task OnInitializedAsync()
    {
        ProjectContext.OnChange += HandleProjectContextChanged;
        await LoadDataAsync();
    }

    private void HandleProjectContextChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await LoadDataAsync();
            StateHasChanged();
        });
    }

    private async Task RefreshProgramDataAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (!ProjectContext.ProgramId.HasValue || ProjectContext.ProgramId.Value == Guid.Empty)
        {
            _isLoading = false;
            _tabs.Clear();
            return;
        }

        try
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
        }
        catch (ObjectDisposedException) { }
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        var currentLoadId = ++_loadId;

        _isLoading = true;
        StateHasChanged();

        try
        {
            var programId = ProjectContext.ProgramId.Value;
            var tenantId = TenantContext.HasActiveTenant ? TenantContext.TenantId : Guid.Empty;

            var healthSummary = await ProgramService.GetProgramHealthAsync(programId, ct);
            ct.ThrowIfCancellationRequested();
            var risks = await ProgramService.GetProgramRisksAsync(programId, ct);
            ct.ThrowIfCancellationRequested();
            var assumptions = await ProgramService.GetProgramAssumptionsAsync(programId, ct);
            ct.ThrowIfCancellationRequested();
            var issues = await ProgramService.GetProgramIssuesAsync(programId, ct);
            ct.ThrowIfCancellationRequested();
            var blockers = await ProgramService.GetProgramBlockersAsync(programId, ct);
            ct.ThrowIfCancellationRequested();

            var now = DateTime.UtcNow;
            var calendarEvents = await CalendarService.GetProgramCalendarAsync(
                tenantId,
                programId,
                now.AddYears(-1),
                now.AddYears(2),
                ct);

            if (currentLoadId != _loadId) return;

            _healthSummary = healthSummary;
            _risks = risks;
            _assumptions = assumptions;
            _issues = issues;
            _blockers = blockers;
            _calendarEvents = calendarEvents;
            _milestones = _calendarEvents
                .Where(e => string.Equals(e.ItemCategory, "Milestone", StringComparison.OrdinalIgnoreCase))
                .ToList();

            BuildTabs();
        }
        catch (OperationCanceledException)
        {
            // Navigation or new load cancelled in-flight query cleanly
        }
        catch (Exception)
        {
            if (currentLoadId == _loadId)
            {
                // Gracefully present tab shell
                BuildTabs();
            }
        }
        finally
        {
            if (currentLoadId == _loadId)
            {
                _isLoading = false;
                StateHasChanged();
            }
        }
    }

    private void BuildTabs()
    {
        var raidCount = _risks.Count + _assumptions.Count + _issues.Count + _blockers.Count;

        _tabs = new List<TabItemModel>
        {
            new TabItemModel
            {
                Id = "home",
                Name = "Executive Summary",
                Icon = CskIcons.Apps,
                Content = builder =>
                {
                    builder.OpenComponent<ProgramHolisticViewHomeTab>(0);
                    builder.AddAttribute(1, nameof(ProgramHolisticViewHomeTab.HealthSummary), _healthSummary);
                    builder.CloseComponent();
                }
            },
            new TabItemModel
            {
                Id = "timeline",
                Name = "Timeline",
                Icon = CskIcons.Clock,
                Content = builder =>
                {
                    builder.OpenComponent<ProgramHolisticViewTimelineTab>(0);
                    builder.AddAttribute(1, nameof(ProgramHolisticViewTimelineTab.ProgramId), ProjectContext.ProgramId.Value);
                    builder.AddAttribute(2, nameof(ProgramHolisticViewTimelineTab.TenantId), TenantContext.HasActiveTenant ? TenantContext.TenantId : Guid.Empty);
                    builder.AddAttribute(3, nameof(ProgramHolisticViewTimelineTab.Events), _calendarEvents);
                    builder.AddAttribute(4, nameof(ProgramHolisticViewTimelineTab.OnDataChanged), EventCallback.Factory.Create(this, RefreshProgramDataAsync));
                    builder.CloseComponent();
                }
            },
            new TabItemModel
            {
                Id = "milestones",
                Name = "Milestones",
                Icon = CskIcons.Flag,
                Badge = _milestones.Count > 0 ? _milestones.Count : null,
                BadgeClass = "csk-badge-secondary",
                Content = builder =>
                {
                    builder.OpenComponent<ProgramHolisticViewMilestoneTab>(0);
                    builder.AddAttribute(1, nameof(ProgramHolisticViewMilestoneTab.Milestones), _milestones);
                    builder.AddAttribute(2, nameof(ProgramHolisticViewMilestoneTab.AllEvents), _calendarEvents);
                    builder.CloseComponent();
                }
            },
            new TabItemModel
            {
                Id = "raid",
                Name = "RAID Log",
                Icon = CskIcons.Shield,
                Badge = raidCount > 0 ? raidCount : null,
                BadgeClass = "csk-badge-error",
                Content = builder =>
                {
                    builder.OpenComponent<ProgramHolisticViewRaidLogTab>(0);
                    builder.AddAttribute(1, nameof(ProgramHolisticViewRaidLogTab.Risks), _risks);
                    builder.AddAttribute(2, nameof(ProgramHolisticViewRaidLogTab.Assumptions), _assumptions);
                    builder.AddAttribute(3, nameof(ProgramHolisticViewRaidLogTab.Issues), _issues);
                    builder.AddAttribute(4, nameof(ProgramHolisticViewRaidLogTab.Blockers), _blockers);
                    builder.CloseComponent();
                }
            }
        };
    }

    public void Dispose()
    {
        ProjectContext.OnChange -= HandleProjectContextChanged;
        try
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
        }
        catch (ObjectDisposedException) { }
    }
}
