using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

/// <summary>
/// A column that renders custom UI for each row in a CskDataGrid.
/// </summary>
public class CskTemplateColumn<TGridItem> : CskColumnBase<TGridItem>
{
    [Parameter]
    public GridSort<TGridItem>? SortBy { get; set; }

    [Parameter]
    public RenderFragment<TGridItem>? ChildContent { get; set; }

    public override GridSort<TGridItem>? SortDefinition => SortBy;

    protected override void OnParametersSet()
    {
        if (SortBy != null)
        {
            Sortable = true;
        }
    }

    public override RenderFragment RenderCell(TGridItem item) => builder =>
    {
        if (ChildContent != null && item != null)
        {
            builder.AddContent(0, ChildContent(item));
        }
    };
}

/// <summary>
/// Backward compatibility alias for CskTemplateColumn.
/// </summary>
public class TemplateColumn<TGridItem> : CskTemplateColumn<TGridItem>
{
}
