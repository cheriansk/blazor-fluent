using BlazorFluent.Core.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlazorFluent.Persistence.Context;

/// <summary>
/// Design-time factory used by EF Core command-line tools (dotnet ef) to scaffold migrations
/// without requiring the ASP.NET Core host or external dependency injection container.
/// </summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Default local development connection string for migration scaffolding
        const string connectionString = "Host=localhost;Port=5432;Database=blazorfluent_db;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        });

        return new AppDbContext(
            optionsBuilder.Options,
            new DesignTimeDateTimeProvider(),
            new DesignTimeTenantContext(),
            serviceProvider: null);
    }

    private sealed class DesignTimeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public bool UseUtc => true;
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public string? TenantId => null;
        public bool IsHost => true;
    }
}
