using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Delegates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorFluent.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public const string CreatedProperty = "Created";
    public const string CreatedByProperty = "CreatedBy";
    public const string UpdatedProperty = "Updated";
    public const string UpdatedByProperty = "UpdatedBy";

    public AuditableEntityInterceptor(ICurrentUser currentUser, IDateTimeProvider dateTimeProvider)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context == null) return;

        var now = _dateTimeProvider.Now;
        var currentUserId = _currentUser.UserId ?? "system";

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
            {
                continue;
            }

            // 1. Strongly typed IAuditableEntity
            if (entry.Entity is IAuditableEntity auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    auditableEntity.Created = now;
                    auditableEntity.CreatedBy = currentUserId;
                    auditableEntity.Updated = null;
                    auditableEntity.UpdatedBy = null;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditableEntity.Updated = now;
                    auditableEntity.UpdatedBy = currentUserId;

                    // Ensure creation audit properties cannot be overwritten
                    entry.Property(nameof(IAuditableEntity.Created)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                }
            }
            else
            {
                // 2. Entities with shadow audit properties
                var hasCreated = entry.Metadata.FindProperty(CreatedProperty) != null;
                var hasCreatedBy = entry.Metadata.FindProperty(CreatedByProperty) != null;
                var hasUpdated = entry.Metadata.FindProperty(UpdatedProperty) != null;
                var hasUpdatedBy = entry.Metadata.FindProperty(UpdatedByProperty) != null;

                if (entry.State == EntityState.Added)
                {
                    if (hasCreated) entry.Property(CreatedProperty).CurrentValue = now;
                    if (hasCreatedBy) entry.Property(CreatedByProperty).CurrentValue = currentUserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (hasUpdated) entry.Property(UpdatedProperty).CurrentValue = now;
                    if (hasUpdatedBy) entry.Property(UpdatedByProperty).CurrentValue = currentUserId;

                    if (hasCreated) entry.Property(CreatedProperty).IsModified = false;
                    if (hasCreatedBy) entry.Property(CreatedByProperty).IsModified = false;
                }
            }
        }
    }
}
