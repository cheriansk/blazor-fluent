using BlazorEnterprise.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class UserTaskCommentEntityConfiguration : IEntityTypeConfiguration<UserTaskCommentEntity>
{
    public void Configure(EntityTypeBuilder<UserTaskCommentEntity> builder)
    {
        builder.ToTable("TaskComments", EntitySchemas.app.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.TaskId)
            .IsRequired();

        builder.Property(c => c.ProjectId)
            .IsRequired();

        builder.Property(c => c.AuthorUserEntityId)
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

        builder.HasOne(c => c.AuthorUser)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_TaskComments_TenantId");

        builder.HasIndex(c => new { c.TenantId, c.ProjectId, c.TaskId })
            .HasDatabaseName("IX_TaskComments_Tenant_Project_Task");

        builder.HasIndex(c => new { c.TaskId, c.CreatedAtUtc })
            .HasDatabaseName("IX_TaskComments_TaskId_CreatedAtUtc");
    }
}
