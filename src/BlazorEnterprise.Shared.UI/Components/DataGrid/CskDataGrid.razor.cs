using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

public partial class CskDataGrid<TGridItem> : ComponentBase
{
    private readonly List<ICskColumn<TGridItem>> _columns = new();
    private ICskColumn<TGridItem>? _currentSortColumn;
    private bool _sortDescending;
    private CancellationTokenSource? _searchCts;

    [Parameter]
    public IQueryable<TGridItem>? Items { get; set; }

    /// <summary>Shows a built-in search box that filters rows across all searchable columns.</summary>
    [Parameter]
    public bool Searchable { get; set; }

    [Parameter]
    public string SearchPlaceholder { get; set; } = "Search...";

    [Parameter]
    public int SearchDebounceMs { get; set; } = 250;

    [Parameter]
    public string SearchText { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> SearchTextChanged { get; set; }

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

    private async Task OnSearchInputAsync(ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? string.Empty;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            if (SearchDebounceMs > 0)
            {
                await Task.Delay(SearchDebounceMs, token);
            }
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await SetSearchTextAsync(text);
    }

    private async Task ClearSearchAsync()
    {
        _searchCts?.Cancel();
        await SetSearchTextAsync(string.Empty);
    }

    private async Task SetSearchTextAsync(string text)
    {
        SearchText = text;
        if (SearchTextChanged.HasDelegate)
        {
            await SearchTextChanged.InvokeAsync(text);
        }
        StateHasChanged();
    }

    private IEnumerable<TGridItem> GetFilteredItems()
    {
        if (Items == null)
        {
            return Enumerable.Empty<TGridItem>();
        }

        if (!Searchable || string.IsNullOrWhiteSpace(SearchText))
        {
            return Items;
        }

        var selectors = _columns.Select(c => c.SearchTextSelector).Where(sel => sel != null).Select(sel => sel!).ToList();
        if (selectors.Count == 0)
        {
            return Items;
        }

        var terms = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Items.AsEnumerable().Where(item =>
            terms.All(term => selectors.Any(sel =>
                (sel(item) ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))));
    }

    private IEnumerable<TGridItem> GetSortedItems()
    {
        var filtered = GetFilteredItems();

        if (_currentSortColumn?.SortDefinition != null)
        {
            return _currentSortColumn.SortDefinition.Apply(filtered.AsQueryable(), _sortDescending);
        }

        return filtered;
    }
}

/// <summary>
/// Backward compatibility alias for CskDataGrid.
/// </summary>
public class FluentDataGrid<TGridItem> : CskDataGrid<TGridItem>
{
}
