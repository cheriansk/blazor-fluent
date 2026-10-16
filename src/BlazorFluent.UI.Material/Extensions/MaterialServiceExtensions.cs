using Microsoft.Extensions.DependencyInjection;

namespace BlazorFluent.UI.Material.Extensions;

/// <summary>
/// Service collection extension methods for registering BlazorFluent Material 3 UI Layer.
/// </summary>
public static class MaterialServiceExtensions
{
    /// <summary>
    /// Registers dependencies required by BlazorFluent.UI.Material components.
    /// </summary>
    public static IServiceCollection AddMaterialUI(this IServiceCollection services)
    {
        // Fluent UI base registration is assumed; add any material-specific state or providers here
        return services;
    }
}
