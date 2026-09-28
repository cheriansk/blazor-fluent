namespace BlazorFluent.Core.Contracts;

public enum ScreenBreakpoint
{
    Mobile = 1,   // < 640px
    Tablet = 2,   // 640px - 1023px
    Desktop = 3   // >= 1024px
}

public record ViewportDimensions(int Width, int Height, ScreenBreakpoint Breakpoint);

public interface ILayoutBreakpointService
{
    ValueTask<ViewportDimensions> GetDimensionsAsync();
    event Action<ViewportDimensions>? OnBreakpointChanged;
}
