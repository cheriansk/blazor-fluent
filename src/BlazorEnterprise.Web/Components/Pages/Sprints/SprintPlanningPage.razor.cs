using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Web.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Web.Components.Pages.Sprints;

public partial class SprintPlanningPage : ComponentBase, IDisposable
{
    private bool _isLoading = true;
    private string? _activeTabId = "summary";
    private List<TabItemModel> _tabs = new();

    private List<SprintDto> _sprints = new();
    private Guid? _selectedSummarySprintId;
    private SprintMetricsDto? _metrics;
    private List<SprintItemDto> _summaryItems = new();

    // Planner Tab State
    private Guid? _selectedPlannerSprintId;
    private List<SprintItemDto> _plannerItems = new();
    private HashSet<Guid> _selectedItemIds = new();
    private List<ResourceCapacityDto> _capacities = new();
    private bool _showMoveModal;
    private string _targetMoveSprintId = string.Empty;
    private string? _moveError;

    protected override async Task OnInitializedAsync()
    {
        ProjectContext.OnChange += HandleProjectChanged;
        await LoadDataAsync();
    }

    private async void HandleProjectChanged()
    {
        await InvokeAsync(async () =>
        {
            await LoadDataAsync();
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        ProjectContext.OnChange -= HandleProjectChanged;
    }

    private async Task LoadDataAsync()
    {
        if (!ProjectContext.HasProject)
        {
            _isLoading = false;
            return;
        }

        try
        {
            _isLoading = true;
            _sprints = await SprintPlanningService.GetProjectSprintsAsync(ProjectContext.ProjectId!.Value);

            if (_selectedSummarySprintId == null && _sprints.Count > 0)
            {
                var active = _sprints.FirstOrDefault(s => s.Status == SprintStatusEnum.Active);
                _selectedSummarySprintId = active?.Id ?? _sprints[0].Id;
            }

            if (_selectedPlannerSprintId == null && _sprints.Count > 0)
            {
                var active = _sprints.FirstOrDefault(s => s.Status == SprintStatusEnum.Active);
                _selectedPlannerSprintId = active?.Id ?? _sprints[0].Id;
            }

            await LoadSummaryTabDataAsync();
            await LoadPlannerTabDataAsync();
            BuildTabs();
        }
        catch (Exception ex)
        {
            // feedback
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadSummaryTabDataAsync()
    {
        _metrics = await SprintPlanningService.GetSprintMetricsAsync(_selectedSummarySprintId, ProjectContext.ProjectId!.Value);
        _summaryItems = await SprintPlanningService.GetSprintItemsAsync(_selectedSummarySprintId, ProjectContext.ProjectId!.Value);
    }

    private async Task LoadPlannerTabDataAsync()
    {
        _plannerItems = await SprintPlanningService.GetSprintItemsAsync(_selectedPlannerSprintId, ProjectContext.ProjectId!.Value);
        _selectedItemIds.Clear();

        var selectedSprint = _sprints.FirstOrDefault(s => s.Id == _selectedPlannerSprintId);
        if (selectedSprint != null)
        {
            _capacities = await SprintPlanningService.CalculateResourceCapacityAsync(
                ProjectContext.ProjectId!.Value,
                selectedSprint.StartDate,
                selectedSprint.EndDate);
        }
        else
        {
            _capacities.Clear();
        }
    }

    private async Task OnSummarySprintChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        if (string.IsNullOrEmpty(val))
        {
            _selectedSummarySprintId = null;
        }
        else if (Guid.TryParse(val, out var parsedId))
        {
            _selectedSummarySprintId = parsedId;
        }
        await LoadSummaryTabDataAsync();
        BuildTabs();
    }

    private async Task OnPlannerSprintChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        if (string.IsNullOrEmpty(val))
        {
            _selectedPlannerSprintId = null;
        }
        else if (Guid.TryParse(val, out var parsedId))
        {
            _selectedPlannerSprintId = parsedId;
        }
        await LoadPlannerTabDataAsync();
        BuildTabs();
    }

    private void ToggleItemSelection(Guid itemId, ChangeEventArgs e)
    {
        if (e.Value is bool isChecked && isChecked)
        {
            _selectedItemIds.Add(itemId);
        }
        else
        {
            _selectedItemIds.Remove(itemId);
        }
    }

    private void ToggleAllItems(ChangeEventArgs e)
    {
        if (e.Value is bool isChecked && isChecked)
        {
            _selectedItemIds = _plannerItems.Select(i => i.ItemId).ToHashSet();
        }
        else
        {
            _selectedItemIds.Clear();
        }
    }

    private void OpenMoveModal()
    {
        if (_selectedItemIds.Count == 0)
        {
            // feedback
            return;
        }

        var activeOrPlanned = _sprints.FirstOrDefault(s => s.Status != SprintStatusEnum.Completed);
        _targetMoveSprintId = activeOrPlanned?.Id.ToString() ?? string.Empty;
        _moveError = null;
        _showMoveModal = true;
    }

    private void CloseMoveModal()
    {
        _showMoveModal = false;
        _moveError = null;
    }

    private async Task ExecuteMoveAsync()
    {
        try
        {
            Guid? targetSprintId = null;
            if (!string.IsNullOrEmpty(_targetMoveSprintId) && Guid.TryParse(_targetMoveSprintId, out var parsedTarget))
            {
                targetSprintId = parsedTarget;
            }

            await SprintPlanningService.MoveItemsToSprintAsync(_selectedItemIds.ToList(), targetSprintId, ProjectContext.ProjectId!.Value);
            _showMoveModal = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _moveError = ex.Message;
        }
    }

    private void BuildTabs()
    {
        _tabs = new List<TabItemModel>
        {
            new TabItemModel
            {
                Id = "summary",
                Name = "Sprint Summary",
                Icon = CskIcons.DocumentBulletList,
                Content = RenderSummaryTab()
            },
            new TabItemModel
            {
                Id = "planner",
                Name = "Sprint Planner",
                Icon = CskIcons.Calendar,
                Content = RenderPlannerTab()
            }
        };
    }

    private RenderFragment RenderSummaryTab() => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "sprint-tab-content");

        // Sprint Selector Header
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "summary-header-row");
        builder.OpenElement(4, "div");
        builder.AddAttribute(5, "class", "selector-group");
        builder.OpenElement(6, "label");
        builder.AddContent(7, "Selected Sprint:");
        builder.CloseElement();
        builder.OpenElement(8, "select");
        builder.AddAttribute(9, "class", "csk-select");
        builder.AddAttribute(10, "value", _selectedSummarySprintId?.ToString() ?? "");
        builder.AddAttribute(11, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, OnSummarySprintChanged));

        builder.OpenElement(12, "option");
        builder.AddAttribute(13, "value", "");
        builder.AddContent(14, "Backlog (Unassigned)");
        builder.CloseElement();

        foreach (var sprint in _sprints)
        {
            builder.OpenElement(15, "option");
            builder.AddAttribute(16, "value", sprint.Id.ToString());
            builder.AddContent(17, $"Sprint {sprint.SprintNumber}: {sprint.SprintName} ({sprint.Status})");
            builder.CloseElement();
        }
        builder.CloseElement(); // select
        builder.CloseElement(); // selector-group
        builder.CloseElement(); // summary-header-row

        // KPI Cards Row
        if (_metrics != null)
        {
            builder.OpenElement(18, "div");
            builder.AddAttribute(19, "class", "kpi-cards-grid");

            RenderKpiCard(builder, 20, "Total Items", _metrics.TotalItemCount.ToString());
            RenderKpiCard(builder, 21, "Planned Hours", $"{_metrics.PlannedHours:N1}h");
            RenderKpiCard(builder, 22, "Completed Hours", $"{_metrics.CompletedHours:N1}h");
            RenderKpiCard(builder, 23, "Blocked Hours", $"{_metrics.BlockedHours:N1}h");
            RenderKpiCard(builder, 24, "Remaining Hours", $"{_metrics.RemainingHours:N1}h");
            RenderKpiCard(builder, 25, "Completion %", $"{_metrics.CompletionPercentage:F0}%");
            RenderKpiCard(builder, 26, "Team Velocity", $"{_metrics.TeamVelocity:N1}h");

            builder.CloseElement(); // kpi-cards-grid
        }

        // Work Items Grid
        builder.OpenElement(27, "div");
        builder.AddAttribute(28, "class", "items-grid-section");
        builder.OpenElement(29, "h4");
        builder.AddAttribute(30, "class", "section-title");
        builder.AddContent(31, $"Work Items ({_summaryItems.Count})");
        builder.CloseElement();

        builder.OpenElement(32, "table");
        builder.AddAttribute(33, "class", "csk-table");
        builder.OpenElement(34, "thead");
        builder.OpenElement(35, "tr");
        builder.OpenElement(36, "th"); builder.AddContent(37, "Title"); builder.CloseElement();
        builder.OpenElement(38, "th"); builder.AddContent(39, "Sprint"); builder.CloseElement();
        builder.OpenElement(40, "th"); builder.AddContent(41, "Status"); builder.CloseElement();
        builder.OpenElement(42, "th"); builder.AddContent(43, "Assignee"); builder.CloseElement();
        builder.OpenElement(44, "th"); builder.AddContent(45, "Est Hours"); builder.CloseElement();
        builder.OpenElement(46, "th"); builder.AddContent(47, "Act Hours"); builder.CloseElement();
        builder.CloseElement(); // tr
        builder.CloseElement(); // thead
        builder.OpenElement(48, "tbody");

        if (_summaryItems.Count == 0)
        {
            builder.OpenElement(49, "tr");
            builder.OpenElement(50, "td");
            builder.AddAttribute(51, "colspan", "6");
            builder.AddAttribute(52, "class", "text-center empty-cell");
            builder.AddContent(53, "No work items found in this iteration.");
            builder.CloseElement();
            builder.CloseElement();
        }
        else
        {
            foreach (var item in _summaryItems)
            {
                builder.OpenElement(54, "tr");
                builder.OpenElement(55, "td");
                builder.AddAttribute(56, "class", "font-weight-medium");
                builder.AddContent(57, item.Title);
                builder.CloseElement();

                builder.OpenElement(58, "td");
                builder.AddContent(59, item.SprintName ?? "Backlog");
                builder.CloseElement();

                builder.OpenElement(60, "td");
                var badgeClass = GetStatusBadgeClass(item.Status);
                builder.OpenElement(61, "span");
                builder.AddAttribute(62, "class", $"status-badge {badgeClass}");
                builder.AddContent(63, item.Status.ToString());
                builder.CloseElement();
                builder.CloseElement();

                builder.OpenElement(64, "td");
                builder.AddContent(65, string.IsNullOrEmpty(item.AssignedToName) ? "Unassigned" : item.AssignedToName);
                builder.CloseElement();

                builder.OpenElement(66, "td");
                builder.AddContent(67, $"{item.EstimatedHours:N1}h");
                builder.CloseElement();

                builder.OpenElement(68, "td");
                builder.AddContent(69, $"{item.ActualHours:N1}h");
                builder.CloseElement();

                builder.CloseElement(); // tr
            }
        }

        builder.CloseElement(); // tbody
        builder.CloseElement(); // table
        builder.CloseElement(); // items-grid-section
        builder.CloseElement(); // sprint-tab-content
    };

    private RenderFragment RenderPlannerTab() => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "sprint-tab-content");

        // Planner Header & Actions
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "planner-header-row");

        builder.OpenElement(4, "div");
        builder.AddAttribute(5, "class", "selector-group");
        builder.OpenElement(6, "label");
        builder.AddContent(7, "Sprint Iteration:");
        builder.CloseElement();
        builder.OpenElement(8, "select");
        builder.AddAttribute(9, "class", "csk-select");
        builder.AddAttribute(10, "value", _selectedPlannerSprintId?.ToString() ?? "");
        builder.AddAttribute(11, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, OnPlannerSprintChanged));

        builder.OpenElement(12, "option");
        builder.AddAttribute(13, "value", "");
        builder.AddContent(14, "Backlog (Ready for Sprint)");
        builder.CloseElement();

        foreach (var sprint in _sprints)
        {
            builder.OpenElement(15, "option");
            builder.AddAttribute(16, "value", sprint.Id.ToString());
            builder.AddContent(17, $"Sprint {sprint.SprintNumber}: {sprint.SprintName} ({sprint.Status})");
            builder.CloseElement();
        }
        builder.CloseElement(); // select
        builder.CloseElement(); // selector-group

        var selectedSprint = _sprints.FirstOrDefault(s => s.Id == _selectedPlannerSprintId);
        var isLocked = selectedSprint?.Status == SprintStatusEnum.Completed;

        builder.OpenElement(18, "div");
        builder.AddAttribute(19, "class", "planner-actions");
        if (isLocked)
        {
            builder.OpenElement(20, "span");
            builder.AddAttribute(21, "class", "locked-notice");
            builder.AddContent(22, "Completed Sprint - Item movement locked.");
            builder.CloseElement();
        }
        else
        {
            builder.OpenElement(23, "button");
            builder.AddAttribute(24, "type", "button");
            builder.AddAttribute(25, "class", "csk-btn csk-btn-primary");
            builder.AddAttribute(26, "onclick", EventCallback.Factory.Create(this, OpenMoveModal));
            builder.AddContent(27, $"Move to Sprint ({_selectedItemIds.Count})");
            builder.CloseElement();
        }
        builder.CloseElement(); // planner-actions
        builder.CloseElement(); // planner-header-row

        // Split Layout: 60% Left Tasks, 40% Right Capacity
        builder.OpenElement(28, "div");
        builder.AddAttribute(29, "class", "split-planner-layout");

        // Left 60%: Tasks Grid
        builder.OpenElement(30, "div");
        builder.AddAttribute(31, "class", "tasks-pane");
        builder.OpenElement(32, "h4");
        builder.AddAttribute(33, "class", "pane-title");
        builder.AddContent(34, $"Tasks in View ({_plannerItems.Count})");
        builder.CloseElement();

        builder.OpenElement(35, "table");
        builder.AddAttribute(36, "class", "csk-table");
        builder.OpenElement(37, "thead");
        builder.OpenElement(38, "tr");
        builder.OpenElement(39, "th");
        builder.AddAttribute(40, "class", "col-check");
        if (!isLocked)
        {
            builder.OpenElement(41, "input");
            builder.AddAttribute(42, "type", "checkbox");
            builder.AddAttribute(43, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, ToggleAllItems));
            builder.CloseElement();
        }
        builder.CloseElement(); // th
        builder.OpenElement(44, "th"); builder.AddContent(45, "Title"); builder.CloseElement();
        builder.OpenElement(46, "th"); builder.AddContent(47, "Assignee"); builder.CloseElement();
        builder.OpenElement(48, "th"); builder.AddContent(49, "Status"); builder.CloseElement();
        builder.OpenElement(50, "th"); builder.AddContent(51, "Est"); builder.CloseElement();
        builder.CloseElement(); // tr
        builder.CloseElement(); // thead
        builder.OpenElement(52, "tbody");

        if (_plannerItems.Count == 0)
        {
            builder.OpenElement(53, "tr");
            builder.OpenElement(54, "td");
            builder.AddAttribute(55, "colspan", "5");
            builder.AddAttribute(56, "class", "text-center empty-cell");
            builder.AddContent(57, "No tasks in this sprint.");
            builder.CloseElement();
            builder.CloseElement();
        }
        else
        {
            foreach (var item in _plannerItems)
            {
                builder.OpenElement(58, "tr");
                builder.OpenElement(59, "td");
                builder.AddAttribute(60, "class", "col-check");
                if (!isLocked)
                {
                    builder.OpenElement(61, "input");
                    builder.AddAttribute(62, "type", "checkbox");
                    builder.AddAttribute(63, "checked", _selectedItemIds.Contains(item.ItemId));
                    builder.AddAttribute(64, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => ToggleItemSelection(item.ItemId, e)));
                    builder.CloseElement();
                }
                builder.CloseElement();

                builder.OpenElement(65, "td");
                builder.AddAttribute(66, "class", "font-weight-medium");
                builder.AddContent(67, item.Title);
                builder.CloseElement();

                builder.OpenElement(68, "td");
                builder.AddContent(69, string.IsNullOrEmpty(item.AssignedToName) ? "Unassigned" : item.AssignedToName);
                builder.CloseElement();

                builder.OpenElement(70, "td");
                builder.OpenElement(71, "span");
                builder.AddAttribute(72, "class", $"status-badge {GetStatusBadgeClass(item.Status)}");
                builder.AddContent(73, item.Status.ToString());
                builder.CloseElement();
                builder.CloseElement();

                builder.OpenElement(74, "td");
                builder.AddContent(75, $"{item.EstimatedHours:N1}h");
                builder.CloseElement();

                builder.CloseElement(); // tr
            }
        }

        builder.CloseElement(); // tbody
        builder.CloseElement(); // table
        builder.CloseElement(); // tasks-pane

        // Right 40%: Resource Capacity
        builder.OpenElement(76, "div");
        builder.AddAttribute(77, "class", "capacity-pane");
        builder.OpenElement(78, "h4");
        builder.AddAttribute(79, "class", "pane-title");
        builder.AddContent(80, "Team Capacity");
        builder.CloseElement();

        if (_selectedPlannerSprintId == null)
        {
            builder.OpenElement(81, "div");
            builder.AddAttribute(82, "class", "empty-capacity");
            builder.AddContent(83, "Select an active or planned sprint to view capacity calculations.");
            builder.CloseElement();
        }
        else
        {
            builder.OpenElement(84, "table");
            builder.AddAttribute(85, "class", "csk-table");
            builder.OpenElement(86, "thead");
            builder.OpenElement(87, "tr");
            builder.OpenElement(88, "th"); builder.AddContent(89, "Resource"); builder.CloseElement();
            builder.OpenElement(90, "th"); builder.AddContent(91, "Role"); builder.CloseElement();
            builder.OpenElement(92, "th"); builder.AddContent(93, "Avail"); builder.CloseElement();
            builder.OpenElement(94, "th"); builder.AddContent(95, "Alloc"); builder.CloseElement();
            builder.OpenElement(96, "th"); builder.AddContent(97, "Rem"); builder.CloseElement();
            builder.CloseElement(); // tr
            builder.CloseElement(); // thead
            builder.OpenElement(98, "tbody");

            if (_capacities.Count == 0)
            {
                builder.OpenElement(99, "tr");
                builder.OpenElement(100, "td");
                builder.AddAttribute(101, "colspan", "5");
                builder.AddAttribute(102, "class", "text-center empty-cell");
                builder.AddContent(103, "No resources assigned to project.");
                builder.CloseElement();
                builder.CloseElement();
            }
            else
            {
                foreach (var cap in _capacities)
                {
                    builder.OpenElement(104, "tr");
                    builder.OpenElement(105, "td");
                    builder.AddAttribute(106, "class", "font-weight-medium");
                    builder.AddContent(107, cap.ResourceName);
                    builder.CloseElement();

                    builder.OpenElement(108, "td");
                    builder.AddContent(109, cap.Role.ToString());
                    builder.CloseElement();

                    builder.OpenElement(110, "td");
                    builder.AddContent(111, $"{cap.AvailableCapacityHours:N1}h");
                    builder.CloseElement();

                    builder.OpenElement(112, "td");
                    builder.AddContent(113, $"{cap.AllocatedHours:N1}h");
                    builder.CloseElement();

                    builder.OpenElement(114, "td");
                    builder.AddAttribute(115, "class", cap.RemainingCapacityHours <= 0 ? "cap-over" : "cap-avail");
                    builder.AddContent(116, $"{cap.RemainingCapacityHours:N1}h");
                    builder.CloseElement();

                    builder.CloseElement(); // tr
                }
            }

            builder.CloseElement(); // tbody
            builder.CloseElement(); // table
        }

        builder.CloseElement(); // capacity-pane
        builder.CloseElement(); // split-planner-layout
        builder.CloseElement(); // sprint-tab-content
    };

    private static void RenderKpiCard(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, int seq, string label, string val)
    {
        builder.OpenElement(seq, "div");
        builder.AddAttribute(seq + 1, "class", "kpi-card");
        builder.OpenElement(seq + 2, "span");
        builder.AddAttribute(seq + 3, "class", "kpi-label");
        builder.AddContent(seq + 4, label);
        builder.CloseElement();
        builder.OpenElement(seq + 5, "span");
        builder.AddAttribute(seq + 6, "class", "kpi-value");
        builder.AddContent(seq + 7, val);
        builder.CloseElement();
        builder.CloseElement();
    }

    private static string GetStatusBadgeClass(TaskStatusEnum status) => status switch
    {
        TaskStatusEnum.Closed => "badge-done",
        TaskStatusEnum.InProgress => "badge-inprogress",
        TaskStatusEnum.Paused => "badge-blocked",
        _ => "badge-todo"
    };
}
