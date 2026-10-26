using Microsoft.Extensions.DependencyInjection;

namespace BlazorEnterprise.Shared.UI.Extensions;

/// <summary>
/// Service registration extensions for the BlazorEnterprise.Shared.UI component library.
/// </summary>
public static class SharedUiServiceExtensions
{
    /// <summary>
    /// Registers shared UI services, theme configurations, and component prerequisites.
    /// </summary>
    public static IServiceCollection AddSharedUI(this IServiceCollection services)
    {
        return services;
    }
}
