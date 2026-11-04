using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Web.Components.Pages.ProjectHolisticViewTabs;
using BlazorEnterprise.Web.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages;

public partial class ProjectHolisticView : ComponentBase, IDisposable
{
    private bool _isLoading = true;
    private int _loadId;
    private CancellationTokenSource? _loadCts;
    private ProjectHealthSummaryDto? _healthSummary;
    private IReadOnlyList<CalendarItemDto> _calendarEvents = Array.Empty<CalendarItemDto>();
    private IReadOnlyList<CalendarItemDto> _milestones = Array.Empty<CalendarItemDto>();
    private IReadOnlyList<RiskEntity> _risks = Array.Empty<RiskEntity>();
    private IReadOnlyList<AssumptionEntity> _assumptions = Array.Empty<AssumptionEntity>();
    private IReadOnlyList<IssueEntity> _issues = Array.Empty<IssueEntity>();
    private IReadOnlyList<WorkDependencyEntity> _blockers = Array.Empty<WorkDependencyEntity>();
    private Dictionary<Guid, int> _linkedWorkItemCounts = new();
    private List<TabItemModel> _tabs = new();

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

    private async Task RefreshProjectDataAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (!ProjectContext.ProjectId.HasValue || ProjectContext.ProjectId.Value == Guid.Empty)
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
            var projectId = ProjectContext.ProjectId.Value;
            var tenantId = TenantContext.HasActiveTenant ? TenantContext.TenantId : Guid.Empty;

            var healthSummary = await ProjectHealthService.GetProjectHealthAsync(projectId, ct);
            ct.ThrowIfCancellationRequested();
            var risks = await ProjectHealthService.GetProjectRisksAsync(projectId, ct);
            ct.ThrowIfCancellationRequested();
            var assumptions = await ProjectHealthService.GetProjectAssumptionsAsync(projectId, ct);
            ct.ThrowIfCancellationRequested();
            var issues = await ProjectHealthService.GetProjectIssuesAsync(projectId, ct);
            ct.ThrowIfCancellationRequested();
            var blockers = await ProjectHealthService.GetProjectBlockersAsync(projectId, ct);
            ct.ThrowIfCancellationRequested();

            var now = DateTime.UtcNow;
            var calendarEvents = await CalendarService.GetProjectCalendarAsync(
                tenantId,
                projectId,
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

            _linkedWorkItemCounts = new Dictionary<Guid, int>();
            foreach (var r in _risks.Where(x => x.MilestoneId.HasValue))
            {
                _linkedWorkItemCounts[r.MilestoneId!.Value] = _linkedWorkItemCounts.GetValueOrDefault(r.MilestoneId.Value) + 1;
            }
            foreach (var a in _assumptions.Where(x => x.MilestoneId.HasValue))
            {
                _linkedWorkItemCounts[a.MilestoneId!.Value] = _linkedWorkItemCounts.GetValueOrDefault(a.MilestoneId.Value) + 1;
            }
            foreach (var i in _issues.Where(x => x.MilestoneId.HasValue))
            {
                _linkedWorkItemCounts[i.MilestoneId!.Value] = _linkedWorkItemCounts.GetValueOrDefault(i.MilestoneId.Value) + 1;
            }
            foreach (var b in _blockers.Where(x => x.MilestoneId.HasValue))
            {
                _linkedWorkItemCounts[b.MilestoneId!.Value] = _linkedWorkItemCounts.GetValueOrDefault(b.MilestoneId.Value) + 1;
            }

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
                    builder.OpenComponent<ProjectHolisticViewHomeTab>(0);
                    builder.AddAttribute(1, nameof(ProjectHolisticViewHomeTab.HealthSummary), _healthSummary);
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
                    builder.OpenComponent<ProjectHolisticViewTimelineTab>(0);
                    builder.AddAttribute(1, nameof(ProjectHolisticViewTimelineTab.ProjectId), ProjectContext.ProjectId.Value);
                    builder.AddAttribute(2, nameof(ProjectHolisticViewTimelineTab.TenantId), TenantContext.HasActiveTenant ? TenantContext.TenantId : Guid.Empty);
                    builder.AddAttribute(3, nameof(ProjectHolisticViewTimelineTab.Events), _calendarEvents);
                    builder.AddAttribute(4, nameof(ProjectHolisticViewTimelineTab.OnDataChanged), EventCallback.Factory.Create(this, RefreshProjectDataAsync));
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
                    builder.OpenComponent<ProjectHolisticViewMilestoneTab>(0);
                    builder.AddAttribute(1, nameof(ProjectHolisticViewMilestoneTab.Milestones), _milestones);
                    builder.AddAttribute(2, nameof(ProjectHolisticViewMilestoneTab.AllEvents), _calendarEvents);
                    builder.AddAttribute(3, nameof(ProjectHolisticViewMilestoneTab.LinkedWorkItemCounts), _linkedWorkItemCounts);
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
                    builder.OpenComponent<ProjectHolisticViewRaidLogTab>(0);
                    builder.AddAttribute(1, nameof(ProjectHolisticViewRaidLogTab.Risks), _risks);
                    builder.AddAttribute(2, nameof(ProjectHolisticViewRaidLogTab.Assumptions), _assumptions);
                    builder.AddAttribute(3, nameof(ProjectHolisticViewRaidLogTab.Issues), _issues);
                    builder.AddAttribute(4, nameof(ProjectHolisticViewRaidLogTab.Blockers), _blockers);
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
