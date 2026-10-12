using BlazorFluent.Core.Contracts;
using Microsoft.JSInterop;

namespace BlazorFluent.Infrastructure.UI;

public class LayoutBreakpointService : ILayoutBreakpointService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<LayoutBreakpointService>? _dotNetRef;

    public event Action<ViewportDimensions>? OnBreakpointChanged;

    public LayoutBreakpointService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask<ViewportDimensions> GetDimensionsAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<int[]>("responsiveInterop.getDimensions");
            return ParseDimensions(result[0], result[1]);
        }
        catch
        {
            return new ViewportDimensions(1280, 800, ScreenBreakpoint.Desktop);
        }
    }

    [JSInvokable]
    public void OnResize(int width, int height)
    {
        var dimensions = ParseDimensions(width, height);
        OnBreakpointChanged?.Invoke(dimensions);
    }

    private static ViewportDimensions ParseDimensions(int width, int height)
    {
        var breakpoint = width switch
        {
            < 640 => ScreenBreakpoint.Mobile,
            < 1024 => ScreenBreakpoint.Tablet,
            _ => ScreenBreakpoint.Desktop
        };
        return new ViewportDimensions(width, height, breakpoint);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef != null)
        {
            _dotNetRef.Dispose();
        }
        await ValueTask.CompletedTask;
    }
}
