namespace BlazorFluent.Components.Common;

/// <summary>
/// Definition of a single column inside a <see cref="FluentSwimlaneBoard{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The type of item presented on the board.</typeparam>
public class SwimlaneColumn<TItem>
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? BadgeColor { get; set; }
    public Func<TItem, bool> Predicate { get; set; } = _ => true;

    public SwimlaneColumn(string title, Func<TItem, bool> predicate, string? badgeColor = null, string? subtitle = null)
    {
        Title = title;
        Predicate = predicate;
        BadgeColor = badgeColor;
        Subtitle = subtitle;
    }
}
