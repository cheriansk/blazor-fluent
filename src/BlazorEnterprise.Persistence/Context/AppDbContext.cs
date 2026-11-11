using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Domain.Resources;
using BlazorEnterprise.Core.Domain.Sprints;
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
    /// Internal flag authorized only when saving through a managed TransactionContext (ITransactionScope).
    /// Direct SaveChanges / SaveChangesAsync calls without this flag set fail-closed.
    /// </summary>
    public bool IsTransactionScopeCommitAuthorized { get; set; }

    /// <summary>
    /// Flag indicating if this DbContext is managed by an ambient TransactionContext.
    /// Prevents premature disposal of shared context during nested service calls.
    /// </summary>
    public bool IsManagedByAmbientScope { get; set; }

    /// <summary>

    /// Explicit whitelist of host-wide global platform entities allowed to omit TenantId.

    /// Any other entity attempting to register without TenantScopedEntity will trigger an immediate startup exception.

    /// </summary>

        public static readonly IReadOnlySet<Type> WhitelistedGlobalEntities = new HashSet<Type>
    {
        typeof(TenantEntity),
        typeof(UserEntity),
        typeof(KnowledgeArticleReviewerEntity),
        typeof(ImpersonationGrantEntity)
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
    public DbSet<ProjectResourceEntity> ProjectResources => Set<ProjectResourceEntity>();
    public DbSet<ResourceAllocationEntity> ResourceAllocations => Set<ResourceAllocationEntity>();
    public DbSet<ResourceRateEntity> ResourceRates => Set<ResourceRateEntity>();

    public DbSet<SprintEntity> Sprints => Set<SprintEntity>();
    public DbSet<SprintAuditLogEntity> SprintAuditLogs => Set<SprintAuditLogEntity>();
    public DbSet<SprintItemVersionEntity> SprintItemVersions => Set<SprintItemVersionEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<JobExecutionEntity> JobExecutions => Set<JobExecutionEntity>();

    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();

    public DbSet<ImpersonationGrantEntity> ImpersonationGrants => Set<ImpersonationGrantEntity>();

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<SprintTaskEntity> SprintTasks => Set<SprintTaskEntity>();
    public DbSet<WorkflowTaskEntity> WorkflowTasks => Set<WorkflowTaskEntity>();
    public DbSet<MiscTaskEntity> MiscTasks => Set<MiscTaskEntity>();
    public DbSet<DeliverableEntity> Deliverables => Set<DeliverableEntity>();

    public DbSet<SubTaskEntity> SubTasks => Set<SubTaskEntity>();

    public DbSet<RiskEntity> Risks => Set<RiskEntity>();

    public DbSet<AssumptionEntity> Assumptions => Set<AssumptionEntity>();

    public DbSet<IssueEntity> Issues => Set<IssueEntity>();

    public DbSet<WorkDependencyEntity> WorkDependencies => Set<WorkDependencyEntity>();
                        public DbSet<CalendarEntity> Calendars => Set<CalendarEntity>();

    

    

    public DbSet<UserCalendarEntity> UserCalendars => Set<UserCalendarEntity>();

    public DbSet<UserTaskCommentEntity> TaskComments => Set<UserTaskCommentEntity>();

    public DbSet<KnowledgeArticleEntity> KnowledgeArticles => Set<KnowledgeArticleEntity>();

    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();

    public DbSet<KnowledgeArticleReviewerEntity> ArticleReviewers => Set<KnowledgeArticleReviewerEntity>();

    public DbSet<EventPublishTrackerEntity> EventPublishTrackers => Set<EventPublishTrackerEntity>();

    public DbSet<EventConsumptionTrackerEntity> EventConsumptionTrackers => Set<EventConsumptionTrackerEntity>();

    public DbSet<ImportFileEntity> ImportFiles => Set<ImportFileEntity>();
    public DbSet<RevisionHeaderEntity> RevisionHeaders => Set<RevisionHeaderEntity>();



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

        // Enforce Zero-Slug multi-tenant column ordering: TenantId is strictly Column 0 on all tenant entities
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantScopedEntity).IsAssignableFrom(entity.ClrType))
            {
                var tenantProp = entity.FindProperty(nameof(TenantScopedEntity.TenantId));
                tenantProp?.SetColumnOrder(0);
            }
        }



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

            // Fail-closed BaseEntity Rule: Every entity MUST inherit BaseEntity to be mapped/persisted
            if (!typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates core domain architecture! " +
                    $"Every entity must inherit from '{nameof(BaseEntity)}'.");
            }

            // Fail-closed TenantScopedEntity Rule: Every entity MUST inherit TenantScopedEntity unless in the 4-entity global whitelist
            if (!typeof(TenantScopedEntity).IsAssignableFrom(clrType) && !WhitelistedGlobalEntities.Contains(clrType))
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates multi-tenant security architecture! " +
                    $"Every entity must extend '{nameof(TenantScopedEntity)}' unless explicitly whitelisted in '{nameof(WhitelistedGlobalEntities)}'.");
            }



            var isGlobal = typeof(IGlobalEntity).IsAssignableFrom(clrType);

            var isTenant = typeof(TenantScopedEntity).IsAssignableFrom(clrType);

            var isSoftDeletable = typeof(ISoftDeletableEntity).IsAssignableFrom(clrType);

            var isHardDeletable = typeof(IHardDeletableEntity).IsAssignableFrom(clrType);

            var isImmutable = typeof(IImmutableEntity).IsAssignableFrom(clrType);

            var isProjectScoped = typeof(ProjectScopedEntity).IsAssignableFrom(clrType);
            var isVersioned = typeof(IVersionedEntity).IsAssignableFrom(clrType);
            var isUnversioned = typeof(IUnversionedEntity).IsAssignableFrom(clrType);

            // Fail-closed Versioning Architecture Rule: every entity must implement either IVersionedEntity OR IUnversionedEntity
            if (!isVersioned && !isUnversioned)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates versioning governance architecture! " +
                    $"It must implement either '{nameof(IVersionedEntity)}' or '{nameof(IUnversionedEntity)}'.");
            }
            if (isVersioned && isUnversioned)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates versioning governance architecture! " +
                    $"It cannot implement both '{nameof(IVersionedEntity)}' and '{nameof(IUnversionedEntity)}'.");
            }



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

            // Fail-closed Non-Overlap Rule: An entity cannot be both IGlobalEntity and TenantScopedEntity
            if (isGlobal && isTenant)
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.Name}' violates architectural non-overlap rules! " +
                    $"It cannot simultaneously implement '{nameof(IGlobalEntity)}' and inherit from '{nameof(TenantScopedEntity)}'.");
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

                    $"It must implement '{nameof(TenantScopedEntity)}' (for tenant-level isolation). " +

                    $"Global entities are strictly limited to the approved platform whitelist.");

            }



            // Fail-closed Project-Scoped Rule: every ProjectScopedEntity MUST also inherit TenantScopedEntity

            if (isProjectScoped && !isTenant)

            {

                throw new InvalidOperationException(

                    $"Entity '{clrType.Name}' implements '{nameof(ProjectScopedEntity)}' but violates multi-tenant architecture by not implementing '{nameof(TenantScopedEntity)}'! " +

                    $"All project-level tables must be strictly partitioned by both TenantId and ProjectId.");

            }



            // Fail-closed Validation Rule: every concrete entity must have a registered IValidator<TEntity>
            if (_serviceProvider is not null && !clrType.IsAbstract)
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
            // Note: In EF Core, Global Query Filters may ONLY be specified on the root entity of an inheritance hierarchy
            var isRootEntity = entityType.BaseType == null;

            if (isTenant)
            {
                if (isRootEntity)
                {
                    var method = typeof(AppDbContext)
                        .GetMethod(nameof(ConfigureNamedTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                        .MakeGenericMethod(clrType);

                    method.Invoke(this, [modelBuilder]);
                }

                var hasTenantIndex = entityType.GetIndexes().Any(idx =>
                    idx.Properties.Count == 1 && idx.Properties[0].Name == nameof(TenantScopedEntity.TenantId));

                if (!hasTenantIndex)
                {
                    modelBuilder.Entity(clrType, builder =>
                    {
                        builder.HasIndex(nameof(TenantScopedEntity.TenantId));
                    });
                }
            }

            if (isSoftDeletable && isRootEntity)
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ConfigureNamedSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);

                method.Invoke(this, [modelBuilder]);
            }



            // Configure PostgreSQL xmin optimistic concurrency token for all BaseEntity models
            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType, builder =>
                {
                    var prop = builder.Property(nameof(BaseEntity.Version));
                    if (Database.IsNpgsql())
                    {
                        prop.IsRowVersion();
                    }
                    else
                    {
                        prop.HasDefaultValue(0u);
                    }
                });

            }
            // Enforce audit columns (Updated is audit-only, not a concurrency token)
            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType, builder =>
                {
                    builder.Property(nameof(BaseEntity.Created))
                        .HasColumnName("Created")
                        .HasColumnType(timestampColumnType)
                        .IsRequired();

                    builder.Property(nameof(BaseEntity.CreatedBy))
                        .HasColumnName("CreatedBy")
                        .HasMaxLength(256)
                        .IsRequired();

                    builder.Property(nameof(BaseEntity.Updated))
                        .HasColumnName("Updated")
                        .HasColumnType(timestampColumnType)
                        .IsRequired();

                    builder.Property(nameof(BaseEntity.UpdatedBy))
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

                entityType.FindProperty(nameof(TenantScopedEntity.TenantId))?

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
            if (isVersioned)
            {
                entityType.FindProperty(nameof(IVersionedEntity.RevisionHeaderId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(IVersionedEntity.RelId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(IVersionedEntity.VerNum))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                                entityType.FindProperty(nameof(IVersionedEntity.ParentRowId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            }

            if (clrType == typeof(RevisionHeaderEntity))
            {
                entityType.FindProperty(nameof(RevisionHeaderEntity.VerNum))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(RevisionHeaderEntity.ScopeLevel))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(RevisionHeaderEntity.FeatureType))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(RevisionHeaderEntity.ProgramId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(RevisionHeaderEntity.ProjectId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(RevisionHeaderEntity.ParentVerNum))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(RevisionHeaderEntity.ParentRevisionHeaderId))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
                entityType.FindProperty(nameof(RevisionHeaderEntity.CreatorEmail))?
                    .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            }

        }

    }



    private void ConfigureNamedTenantFilter<TEntity>(ModelBuilder modelBuilder)

        where TEntity : TenantScopedEntity

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


    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (BlazorEnterprise.Persistence.Services.AmbientTransactionContext.Current is { } scope && ReferenceEquals(scope.DbContext, this))
        {
            scope.IngestTrackedEntityDomainEvents();
        }
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (BlazorEnterprise.Persistence.Services.AmbientTransactionContext.Current is { } scope && ReferenceEquals(scope.DbContext, this))
        {
            scope.IngestTrackedEntityDomainEvents();
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override void Dispose()
    {
        if (IsManagedByAmbientScope) return;
        base.Dispose();
    }

    public override ValueTask DisposeAsync()
    {
        if (IsManagedByAmbientScope) return ValueTask.CompletedTask;
        return base.DisposeAsync();
    }
}