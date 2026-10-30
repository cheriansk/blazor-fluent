using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorEnterprise.Shared.UI.Components;

/// <summary>
/// A column that displays the value of a property on TGridItem.
/// </summary>
public class CskPropertyColumn<TGridItem, TProperty> : CskColumnBase<TGridItem>
{
    private Func<TGridItem, TProperty>? _compiledGetter;
    private GridSort<TGridItem>? _sort;

    [Parameter]
    public Expression<Func<TGridItem, TProperty>> Property { get; set; } = default!;

    [Parameter]
    public string? Format { get; set; }

    public override GridSort<TGridItem>? SortDefinition => _sort;

    protected override void OnParametersSet()
    {
        if (Property != null)
        {
            _compiledGetter = Property.Compile();

            if (string.IsNullOrEmpty(Title))
            {
                if (Property.Body is MemberExpression memberExpr)
                {
                    Title = memberExpr.Member.Name;
                }
                else if (Property.Body is UnaryExpression { Operand: MemberExpression innerMember })
                {
                    Title = innerMember.Member.Name;
                }
            }

            if (Sortable)
            {
                _sort = GridSort<TGridItem>.ByAscending(Property);
            }
        }
    }

    public override Func<TGridItem, string?>? SearchTextSelector => Searchable && _compiledGetter != null ? GetText : null;

    private string GetText(TGridItem item)
    {
        if (_compiledGetter == null || item == null)
        {
            return string.Empty;
        }

        var value = _compiledGetter(item);
        return value switch
        {
            null => string.Empty,
            IFormattable formattable when !string.IsNullOrEmpty(Format) => formattable.ToString(Format, null),
            _ => value.ToString() ?? string.Empty
        };
    }

    public override RenderFragment RenderCell(TGridItem item) => builder =>
    {
        if (_compiledGetter == null || item == null)
        {
            return;
        }

        builder.AddContent(0, GetText(item));
    };
}

/// <summary>
/// Backward compatibility alias for CskPropertyColumn.
/// </summary>
public class PropertyColumn<TGridItem, TProperty> : CskPropertyColumn<TGridItem, TProperty>
{
}
