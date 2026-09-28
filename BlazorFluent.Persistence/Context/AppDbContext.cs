using System.Reflection;
using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Domain.Catalog;
using BlazorFluent.Core.Domain.Delegates;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Interceptors;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Context;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITenantContext _tenantContext;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IDateTimeProvider dateTimeProvider,
        ITenantContext tenantContext) : base(options)
    {
        _dateTimeProvider = dateTimeProvider;
        _tenantContext = tenantContext;
    }

    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ProjectUserRoleEntity> ProjectUserRoles => Set<ProjectUserRoleEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<JobExecutionEntity> JobExecutions => Set<JobExecutionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Discovers and applies all IEntityTypeConfiguration classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // 2. Determine column type based on timezone configuration
        var timestampColumnType = _dateTimeProvider.UseUtc
            ? "timestamp with time zone"
            : "timestamp without time zone";

        // 3. Enforce multi-tenant security architecture and audit columns on EVERY entity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsKeyless) continue;

            var clrType = entityType.ClrType;
            if (clrType == typeof(DataProtectionKey)) continue;

            var isGlobal = typeof(IGlobalEntity).IsAssignableFrom(clrType);
            var isTenant = typeof(ITenantEntity).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletableEntity).IsAssignableFrom(clrType);

            // Fail-closed Tenancy Rule: every entity MUST declare its tenancy boundary
            if (!isGlobal && !isTenant)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates multi-tenant security architecture! " +
                    $"It must either implement '{nameof(ITenantEntity)}' (for tenant-level isolation) " +
                    $"or explicitly implement '{nameof(IGlobalEntity)}' (if it is host-wide).");
            }

            // Apply EF Core 10 Named Global Query Filters & Indexes
            if (isTenant)
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ConfigureNamedTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, [modelBuilder]);

                modelBuilder.Entity(clrType, builder =>
                {
                    builder.HasIndex(nameof(ITenantEntity.TenantId));
                });
            }

            if (isSoftDeletable)
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ConfigureNamedSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, [modelBuilder]);
            }

            // Enforce audit columns
            if (typeof(IAuditableEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType, builder =>
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
                modelBuilder.Entity(clrType, builder =>
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

    private void ConfigureNamedTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(QueryFilters.Tenant, e => _tenantContext.IsHost || e.TenantId == _tenantContext.TenantId);
    }

    private void ConfigureNamedSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletableEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(QueryFilters.SoftDelete, e => !e.IsDeleted);
    }
}
