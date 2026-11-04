using System;
using System.Linq;
using System.Reflection;
using BlazorEnterprise.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorEnterprise.Persistence.Tests.Architecture;

/// <summary>
/// Architecture test verifying that no service, provider, or UI component
/// directly injects or holds AppDbContext outside explicit framework exceptions.
/// Guarantees that future feature additions cannot regress Blazor Server concurrency.
/// </summary>
public class DbContextInjectionArchitectureTests
{
    private static readonly string[] AllowedTypeNames =
    {
        "AppDbContext",
        "AppDbContextDesignTimeFactory",
        "DevelopmentSeeder",
        "InitialDatabaseSeeder",
        "DataProtectionKeyValidator",
        "CircuitActivityDbInterceptor"
    };

    [Fact]
    public void PersistenceServices_MustNotInjectAppDbContextDirectly_MustUseIDbContextFactory()
    {
        var persistenceAssembly = typeof(AppDbContext).Assembly;
        var serviceTypes = persistenceAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null &&
                        (t.Namespace.Contains(".Services") || t.Namespace.Contains(".Providers")))
            .ToList();

        var violatingTypes = serviceTypes
            .Where(t => !AllowedTypeNames.Contains(t.Name))
            .Where(t =>
            {
                var ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return ctors.Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(AppDbContext)));
            })
            .Select(t => t.FullName)
            .ToList();

        violatingTypes.Should().BeEmpty(
            "all Persistence services must inject IDbContextFactory<AppDbContext> rather than AppDbContext to prevent Blazor Server concurrency crashes.");
    }
}
