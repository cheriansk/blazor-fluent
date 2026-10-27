using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

/// <summary>
/// Interface for columns in a CskDataGrid.
/// </summary>
public interface ICskColumn<TGridItem>
{
    string? Title { get; }
    bool Sortable { get; }
    string? Class { get; }
    GridSort<TGridItem>? SortDefinition { get; }
    RenderFragment RenderHeader(bool isCurrentSort, bool isDescending);
    RenderFragment RenderCell(TGridItem item);
}
