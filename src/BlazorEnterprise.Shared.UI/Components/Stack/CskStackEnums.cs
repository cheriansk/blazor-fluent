namespace BlazorEnterprise.Shared.UI.Components;

/// <summary>
/// Stack layout flex direction.
/// </summary>
public enum CskOrientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Horizontal alignment mapping to justify-content (for horizontal) or align-items (for vertical).
/// </summary>
public enum CskHorizontalAlignment
{
    Left,
    Start = Left,
    FlexStart = Left,
    Center,
    Right,
    End = Right,
    FlexEnd = Right,
    SpaceBetween,
    SpaceAround,
    Stretch
}

/// <summary>
/// Vertical alignment mapping to align-items (for horizontal) or justify-content (for vertical).
/// </summary>
public enum CskVerticalAlignment
{
    Top,
    Start = Top,
    FlexStart = Top,
    Center,
    Bottom,
    End = Bottom,
    FlexEnd = Bottom,
    Stretch,
    SpaceBetween
}

/// <summary>
/// Preset gap sizes for CskStack.
/// </summary>
public enum CskStackGap
{
    None,
    Small,
    Medium,
    Large
}
