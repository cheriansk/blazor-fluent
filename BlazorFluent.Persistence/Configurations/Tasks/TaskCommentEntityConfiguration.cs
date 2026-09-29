using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class TaskCommentEntityConfiguration : IEntityTypeConfiguration<TaskCommentEntity>
{
    public void Configure(EntityTypeBuilder<TaskCommentEntity> builder)
    {
        builder.ToTable("TaskComments", EntitySchemas.app.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.TaskId)
            .IsRequired();

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
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        // Soft delete properties
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_TaskComments_TenantId");

        builder.HasIndex(c => new { c.TaskId, c.CreatedAtUtc })
            .HasDatabaseName("IX_TaskComments_TaskId_CreatedAtUtc");
    }
}
