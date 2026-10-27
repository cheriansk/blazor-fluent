using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskDataGrid<TGridItem> : ComponentBase
{
    private readonly List<ICskColumn<TGridItem>> _columns = new();
    private ICskColumn<TGridItem>? _currentSortColumn;
    private bool _sortDescending;

    [Parameter]
    public IQueryable<TGridItem>? Items { get; set; }

    [Parameter]
    public bool ResizableColumns { get; set; }

    [Parameter]
    public string? GridTemplateColumns { get; set; }

    [Parameter]
    public bool Hover { get; set; } = true;

    [Parameter]
    public bool Dense { get; set; }

    [Parameter]
    public bool Outlined { get; set; } = true;

    [Parameter]
    public int Elevation { get; set; }

    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public RenderFragment? EmptyContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    internal void AddColumn(ICskColumn<TGridItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            StateHasChanged();
        }
    }

    internal void RemoveColumn(ICskColumn<TGridItem> column)
    {
        if (_columns.Remove(column))
        {
            StateHasChanged();
        }
    }

    private async Task HandleHeaderClickAsync(ICskColumn<TGridItem> column)
    {
        if (!column.Sortable)
        {
            return;
        }

        if (_currentSortColumn == column)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _currentSortColumn = column;
            _sortDescending = false;
        }

        await Task.CompletedTask;
        StateHasChanged();
    }

    private IEnumerable<TGridItem> GetSortedItems()
    {
        if (Items == null)
        {
            return Enumerable.Empty<TGridItem>();
        }

        if (_currentSortColumn?.SortDefinition != null)
        {
            return _currentSortColumn.SortDefinition.Apply(Items, _sortDescending);
        }

        return Items;
    }
}

/// <summary>
/// Backward compatibility alias for CskDataGrid.
/// </summary>
public class FluentDataGrid<TGridItem> : CskDataGrid<TGridItem>
{
}
