using Microsoft.Extensions.DependencyInjection;

namespace BlazorEnterprise.UI.Material.Extensions;

/// <summary>
/// Service collection extension methods for registering BlazorEnterprise Material 3 UI Layer.
/// </summary>
public static class MaterialServiceExtensions
{
    /// <summary>
    /// Registers dependencies required by BlazorEnterprise.UI.Material components.
    /// </summary>
    public static IServiceCollection AddMaterialUI(this IServiceCollection services)
    {
        // Fluent UI base registration is assumed; add any material-specific state or providers here
        return services;
    }
}
