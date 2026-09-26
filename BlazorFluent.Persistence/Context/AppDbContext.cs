using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Catalog;
using BlazorFluent.Core.Domain.Delegates;
using BlazorFluent.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Context;

public class AppDbContext : DbContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IDateTimeProvider dateTimeProvider) : base(options)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Discovers and applies all IEntityTypeConfiguration classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // 2. Determine column type based on timezone configuration
        var timestampColumnType = _dateTimeProvider.UseUtc
            ? "timestamp with time zone"
            : "timestamp without time zone";

        // 3. Enforce audit columns (Created, CreatedBy, Updated, UpdatedBy) on EVERY entity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsKeyless) continue;

            if (typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType, builder =>
                {
                    builder.Property(nameof(IAuditableEntity.Created))
                        .HasColumnName("Created")
                        .HasColumnType(timestampColumnType)
                        .IsRequired();

                    builder.Property(nameof(IAuditableEntity.CreatedBy))
                        .HasColumnName("CreatedBy")
                        .HasMaxLength(256)
                        .IsRequired();

                    builder.Property(nameof(IAuditableEntity.Updated))
                        .HasColumnName("Updated")
                        .HasColumnType(timestampColumnType)
                        .IsRequired(false);

                    builder.Property(nameof(IAuditableEntity.UpdatedBy))
                        .HasColumnName("UpdatedBy")
                        .HasMaxLength(256)
                        .IsRequired(false);
                });
            }
            else
            {
                // Inject shadow properties so tables always persist audit columns
                modelBuilder.Entity(entityType.ClrType, builder =>
                {
                    builder.Property<DateTime>(AuditableEntityInterceptor.CreatedProperty)
                        .HasColumnName("Created")
                        .HasColumnType(timestampColumnType)
                        .IsRequired();

                    builder.Property<string>(AuditableEntityInterceptor.CreatedByProperty)
                        .HasColumnName("CreatedBy")
                        .HasMaxLength(256)
                        .IsRequired();

                    builder.Property<DateTime?>(AuditableEntityInterceptor.UpdatedProperty)
                        .HasColumnName("Updated")
                        .HasColumnType(timestampColumnType)
                        .IsRequired(false);

                    builder.Property<string?>(AuditableEntityInterceptor.UpdatedByProperty)
                        .HasColumnName("UpdatedBy")
                        .HasMaxLength(256)
                        .IsRequired(false);
                });
            }
        }
    }
}
