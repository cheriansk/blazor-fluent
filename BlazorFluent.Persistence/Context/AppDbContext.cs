using System.Reflection;
using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Domain.Catalog;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Events;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Core.Domain.Knowledge;
using BlazorFluent.Core.Domain.Notifications;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Interceptors;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Context;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    /// <summary>
    /// Explicit whitelist of host-wide global platform entities allowed to omit TenantId.
    /// Any other entity attempting to register without ITenantEntity will trigger an immediate startup exception.
    /// </summary>
    private static readonly HashSet<Type> WhitelistedGlobalEntities = new()
    {
        typeof(TenantEntity),
        typeof(UserEntity),
        typeof(KnowledgeArticleReviewerEntity),
        typeof(ImpersonationGrantEntity),
        typeof(JobExecutionEntity)
    };

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IServiceProvider? _serviceProvider;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IDateTimeProvider dateTimeProvider,
        ITenantContext tenantContext,
        ICurrentUser? currentUser = null,
        IServiceProvider? serviceProvider = null) : base(options)
    {
        _dateTimeProvider = dateTimeProvider;
        _tenantContext = tenantContext;
        _currentUser = currentUser ?? new DefaultCurrentUser();
        _serviceProvider = serviceProvider;
    }

    public IDateTimeProvider DateTimeProvider => _dateTimeProvider;
    public ITenantContext TenantContext => _tenantContext;
    public ICurrentUser CurrentUser => _currentUser;
    public IServiceProvider? ServiceProvider => _serviceProvider;

    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ProjectUserRoleEntity> ProjectUserRoles => Set<ProjectUserRoleEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<JobExecutionEntity> JobExecutions => Set<JobExecutionEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();
    public DbSet<ImpersonationGrantEntity> ImpersonationGrants => Set<ImpersonationGrantEntity>();
    public DbSet<UserTaskEntity> Tasks => Set<UserTaskEntity>();
    public DbSet<UserTaskCommentEntity> TaskComments => Set<UserTaskCommentEntity>();
    public DbSet<KnowledgeArticleEntity> KnowledgeArticles => Set<KnowledgeArticleEntity>();
    public DbSet<KnowledgeArticleReviewerEntity> ArticleReviewers => Set<KnowledgeArticleReviewerEntity>();
    public DbSet<EventPublishTrackerEntity> EventPublishTrackers => Set<EventPublishTrackerEntity>();
    public DbSet<EventConsumptionTrackerEntity> EventConsumptionTrackers => Set<EventConsumptionTrackerEntity>();
    public DbSet<ImportFileEntity> ImportFiles => Set<ImportFileEntity>();
    public DbSet<StagedTaskEntity> StagedTasks => Set<StagedTaskEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }

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
            var isProjectScoped = typeof(IProjectScopedEntity).IsAssignableFrom(clrType);

            // Fail-closed Global Whitelist Guard: Only approved platform tables may omit TenantId
            if (isGlobal && !WhitelistedGlobalEntities.Contains(clrType))
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' implements '{nameof(IGlobalEntity)}' but is NOT in the approved platform global entity whitelist! " +
                    $"Only approved global platform tables (Tenants, Users, ArticleReviewers, ImpersonationGrants, JobExecutions) may omit TenantId.");
            }

            // Fail-closed Tenancy Rule: every entity MUST declare its tenancy boundary
            if (!isGlobal && !isTenant)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates multi-tenant security architecture! " +
                    $"It must implement '{nameof(ITenantEntity)}' (for tenant-level isolation). " +
                    $"Global entities are strictly limited to the approved platform whitelist.");
            }

            // Fail-closed Project-Scoped Rule: every IProjectScopedEntity MUST also implement ITenantEntity
            if (isProjectScoped && !isTenant)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' implements '{nameof(IProjectScopedEntity)}' but violates multi-tenant architecture by not implementing '{nameof(ITenantEntity)}'! " +
                    $"All project-level tables must be strictly partitioned by both TenantId and ProjectId.");
            }

            // Fail-closed Validation Rule: every business entity must have a registered IValidator<TEntity>
            // unless explicitly tagged with IValidationExemptEntity (e.g. AuditRecordEntity, JobExecutionEntity).
            var isValidationExempt = typeof(IValidationExemptEntity).IsAssignableFrom(clrType);
            if (!isValidationExempt && _serviceProvider is not null)
            {
                var validatorType = typeof(IValidator<>).MakeGenericType(clrType);
                if (_serviceProvider.GetService(validatorType) is null)
                {
                    throw new InvalidOperationException(
                        $"Entity '{clrType.Name}' violates the mandatory entity validation architecture! " +
                        $"It does not implement '{nameof(IValidationExemptEntity)}' and lacks a registered 'IValidator<{clrType.Name}>' in DI. " +
                        $"Every business entity must have a companion validator class or explicitly implement '{nameof(IValidationExemptEntity)}'.");
                }
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
        if (typeof(TEntity) == typeof(KnowledgeArticleEntity))
        {
            modelBuilder.Entity<KnowledgeArticleEntity>()
                .HasQueryFilter(QueryFilters.Tenant, e => _tenantContext.IsHost || e.IsGlobal || e.TenantId == _tenantContext.TenantId);
            return;
        }

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
