using BlazorEnterprise.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class ActionItemEntityConfiguration : IEntityTypeConfiguration<ActionItemEntity>
{
    public void Configure(EntityTypeBuilder<ActionItemEntity> builder)
    {
        builder.ToTable("ActionItems", EntitySchemas.tasks.ToString());

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .HasColumnOrder(0)
            .IsRequired();

        builder.Property(a => a.Level)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.ParentTaskId)
            .IsRequired();

        builder.Property(a => a.ParentSubTaskId)
            .IsRequired(false);

        builder.Property(a => a.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.Complexity)
            .IsRequired();

        builder.Property(a => a.EstimatedHours)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.OrderIndex)
            .IsRequired();

        builder.Property(a => a.IsBlocked)
            .IsRequired();

        builder.Property(a => a.BlockingReason)
            .HasMaxLength(1000)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(a => a.IsDeleted)
            .IsRequired();

        builder.Property(a => a.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(a => a.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Foreign Key Relationships:
        // Intentionally ignoring navigation to polymorphic unmapped BaseTaskEntity in EF model,
        // mirroring SubTaskEntity and UserTaskCommentEntity patterns.

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("IX_ActionItems_TenantId");

        builder.HasIndex(a => a.ParentTaskId)
            .HasDatabaseName("IX_ActionItems_ParentTaskId");

        builder.HasIndex(a => new { a.ParentSubTaskId, a.OrderIndex })
            .HasDatabaseName("IX_ActionItems_ParentSubTaskId_Order");

        builder.HasIndex(a => new { a.TenantId, a.Status })
            .HasDatabaseName("IX_ActionItems_Tenant_Status");
    }
}
