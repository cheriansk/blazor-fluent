using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class WorkItemCommentEntityConfiguration : IEntityTypeConfiguration<WorkItemCommentEntity>
{
    public void Configure(EntityTypeBuilder<WorkItemCommentEntity> builder)
    {
        builder.ToTable("WorkItemComments", EntitySchemas.tasks.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.TargetItemId)
            .IsRequired();

        builder.Property(c => c.TargetItemType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.AuthorUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.AuthorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.AuthorEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.CommentText)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        // Soft delete properties
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(c => c.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_WorkItemComments_TenantId");

        builder.HasIndex(c => new { c.TargetItemId, c.TargetItemType })
            .HasDatabaseName("IX_WorkItemComments_Target");

        builder.HasIndex(c => new { c.TargetItemId, c.CreatedAtUtc })
            .HasDatabaseName("IX_WorkItemComments_Target_CreatedAtUtc");
    }
}
