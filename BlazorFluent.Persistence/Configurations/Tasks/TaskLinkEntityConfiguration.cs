using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class TaskLinkEntityConfiguration : IEntityTypeConfiguration<TaskLinkEntity>
{
    public void Configure(EntityTypeBuilder<TaskLinkEntity> builder)
    {
        builder.ToTable("TaskLinks", EntitySchemas.tasks.ToString());

        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.SourceTaskId)
            .IsRequired();

        builder.Property(l => l.TargetTaskId)
            .IsRequired();

        builder.Property(l => l.LinkType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(l => l.BlockerDependencyId)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(l => l.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(l => l.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(l => l.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Foreign keys to Source, Target and BlockerDependency
        builder.HasOne(l => l.SourceTask)
            .WithMany(t => t.OutgoingLinks)
            .HasForeignKey(l => l.SourceTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.TargetTask)
            .WithMany(t => t.IncomingLinks)
            .HasForeignKey(l => l.TargetTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.BlockerDependency)
            .WithMany()
            .HasForeignKey(l => l.BlockerDependencyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(l => l.TenantId)
            .HasDatabaseName("IX_TaskLinks_TenantId");

        builder.HasIndex(l => new { l.SourceTaskId, l.TargetTaskId, l.LinkType })
            .HasDatabaseName("IX_TaskLinks_Source_Target_Type");

        builder.HasIndex(l => l.BlockerDependencyId)
            .HasDatabaseName("IX_TaskLinks_BlockerDependencyId");
    }
}
