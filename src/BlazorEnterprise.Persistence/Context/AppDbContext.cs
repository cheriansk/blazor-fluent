using System.Reflection;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Auditing;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Events;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Imports;
using BlazorEnterprise.Core.Domain.Jobs;
using BlazorEnterprise.Core.Domain.Knowledge;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Notifications;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Persistence.Interceptors;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using BlazorEnterprise.Core.Filters;

namespace BlazorEnterprise.Persistence.Context;

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

    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<ProgramEntity> Programs => Set<ProgramEntity>();
    public DbSet<ProgramUserRoleEntity> ProgramUserRoles => Set<ProgramUserRoleEntity>();
    public DbSet<TenantUserEntity> TenantUsers => Set<TenantUserEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ProjectUserRoleEntity> ProjectUserRoles => Set<ProjectUserRoleEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<JobExecutionEntity> JobExecutions => Set<JobExecutionEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();
    public DbSet<ImpersonationGrantEntity> ImpersonationGrants => Set<ImpersonationGrantEntity>();
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<SubTaskEntity> SubTasks => Set<SubTaskEntity>();
    public DbSet<RiskEntity> Risks => Set<RiskEntity>();
    public DbSet<AssumptionEntity> Assumptions => Set<AssumptionEntity>();
    public DbSet<IssueEntity> Issues => Set<IssueEntity>();
    public DbSet<WorkDependencyEntity> WorkDependencies => Set<WorkDependencyEntity>();
    public DbSet<ProgramCalendarEntity> ProgramCalendars => Set<ProgramCalendarEntity>();
    public DbSet<ProjectCalendarEntity> ProjectCalendars => Set<ProjectCalendarEntity>();
    public DbSet<UserCalendarEntity> UserCalendars => Set<UserCalendarEntity>();
    public DbSet<UserTaskCommentEntity> TaskComments => Set<UserTaskCommentEntity>();
    public DbSet<KnowledgeArticleEntity> KnowledgeArticles => Set<KnowledgeArticleEntity>();
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<KnowledgeArticleReviewerEntity> ArticleReviewers => Set<KnowledgeArticleReviewerEntity>();
    public DbSet<EventPublishTrackerEntity> EventPublishTrackers => Set<EventPublishTrackerEntity>();
    public DbSet<EventConsumptionTrackerEntity> EventConsumptionTrackers => Set<EventConsumptionTrackerEntity>();
    public DbSet<ImportFileEntity> ImportFiles => Set<ImportFileEntity>();

    public DbSet<StagedImportEntity> StagedImports => Set<StagedImportEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("timestamp with time zone")
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveColumnType("timestamp with time zone")
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
            var isHardDeletable = typeof(IHardDeletableEntity).IsAssignableFrom(clrType);
            var isImmutable = typeof(IImmutableEntity).IsAssignableFrom(clrType);
            var isProjectScoped = typeof(IProjectScopedEntity).IsAssignableFrom(clrType);

            // Fail-closed Deletion Lifecycle Rule: every entity must implement exactly ONE deletion marker
            var markerCount = (isSoftDeletable ? 1 : 0) + (isHardDeletable ? 1 : 0) + (isImmutable ? 1 : 0);
            if (markerCount == 0)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates the deletion lifecycle architecture! " +
                    $"It must implement exactly one deletion marker interface: " +
                    $"'{nameof(ISoftDeletableEntity)}', '{nameof(IHardDeletableEntity)}', or '{nameof(IImmutableEntity)}'.");
            }
            if (markerCount > 1)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates the deletion lifecycle architecture! " +
                    $"It implements multiple conflicting deletion markers. " +
                    $"An entity must implement at most ONE of '{nameof(ISoftDeletableEntity)}', '{nameof(IHardDeletableEntity)}', or '{nameof(IImmutableEntity)}'.");
            }

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

            // Fail-closed Validation Rule: every entity must have a registered IValidator<TEntity>
            if (_serviceProvider is not null)
            {
                var validatorType = typeof(IValidator<>).MakeGenericType(clrType);
                if (_serviceProvider.GetService(validatorType) is null)
                {
                    throw new InvalidOperationException(
                        $"Entity '{clrType.Name}' violates the mandatory entity validation architecture! " +
                        $"It lacks a registered 'IValidator<{clrType.Name}>' in DI. " +
                        $"Every business entity must have a companion validator class.");
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
                        .IsRequired();

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

            // 4. Hard Immutability Barrier: Level, TenantId, ProgramId, ProjectId cannot be mutated once created
            if (isTenant)
            {
                entityType.FindProperty(nameof(ITenantEntity.TenantId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            }

            if (typeof(BaseTaskEntity).IsAssignableFrom(clrType))
            {
                entityType.FindProperty(nameof(BaseTaskEntity.Level))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(BaseTaskEntity.ProgramId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(BaseTaskEntity.ProjectId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            }
        }
    }

    private void ConfigureNamedTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        if (typeof(TEntity) == typeof(KnowledgeArticleEntity))
        {
            modelBuilder.Entity<KnowledgeArticleEntity>()
                .HasQueryFilter(QueryFilters.Tenant, e => _tenantContext.HasActiveTenant && (e.IsGlobal || e.TenantId == _tenantContext.TenantId));
            return;
        }

        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(QueryFilters.Tenant, e => _tenantContext.HasActiveTenant && e.TenantId == _tenantContext.TenantId);
    }

    private void ConfigureNamedSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletableEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(QueryFilters.SoftDelete, e => !e.IsDeleted);
    }
}
