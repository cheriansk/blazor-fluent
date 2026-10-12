using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Persistence.Interceptors;
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

        var dateTimeProvider = new ConfigurableDateTimeProvider(useUtc: true);
        var tenantContext = new TenantContext();
        tenantContext.Initialize(
            tenantId: null,
            tenantName: null,
            userType: UserType.CompanyUser,
            allowedTenants: Array.Empty<TenantInfo>(),
            isHost: true);

        optionsBuilder.AddInterceptors(new TenantDbConnectionInterceptor(tenantContext));

        return new AppDbContext(
            optionsBuilder.Options,
            dateTimeProvider,
            tenantContext,
            serviceProvider: null);
    }
}
