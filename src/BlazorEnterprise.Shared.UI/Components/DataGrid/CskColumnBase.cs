using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

/// <summary>
/// Abstract base class for CskDataGrid columns.
/// </summary>
public abstract class CskColumnBase<TGridItem> : ComponentBase, ICskColumn<TGridItem>, IDisposable
{
    [CascadingParameter]
    protected CskDataGrid<TGridItem>? Grid { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public bool Sortable { get; set; }

    [Parameter]
    public string? Class { get; set; }

    /// <summary>Set to false to exclude this column from the grid's built-in search.</summary>
    [Parameter]
    public bool Searchable { get; set; } = true;

    public virtual GridSort<TGridItem>? SortDefinition => null;

    public virtual Func<TGridItem, string?>? SearchTextSelector => null;

    protected override void OnInitialized()
    {
        Grid?.AddColumn(this);
    }

    public virtual RenderFragment RenderHeader(bool isCurrentSort, bool isDescending) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "csk-th-content");

        builder.OpenElement(2, "span");
        builder.AddAttribute(3, "class", "csk-th-title");
        builder.AddContent(4, Title ?? string.Empty);
        builder.CloseElement();

        if (Sortable && isCurrentSort)
        {
            builder.OpenElement(5, "span");
            builder.AddAttribute(6, "class", isDescending ? "csk-sort-icon csk-sort-desc" : "csk-sort-icon csk-sort-asc");
            builder.AddMarkupContent(7, $"<svg class=\"csk-sort-svg\" viewBox=\"0 0 24 24\"><path d=\"{(isDescending ? CskIcons.ArrowDownward : CskIcons.ArrowUpward)}\" /></svg>");
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    public abstract RenderFragment RenderCell(TGridItem item);

    public void Dispose()
    {
        Grid?.RemoveColumn(this);
        GC.SuppressFinalize(this);
    }
}
