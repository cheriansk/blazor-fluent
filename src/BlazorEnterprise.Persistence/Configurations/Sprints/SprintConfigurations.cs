using BlazorEnterprise.Core.Domain.Sprints;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Sprints;

public class SprintEntityConfiguration : IEntityTypeConfiguration<SprintEntity>
{
    public void Configure(EntityTypeBuilder<SprintEntity> builder)
    {
        builder.ToTable("Sprints", EntitySchemas.tasks.ToString());
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SprintName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(1000);

        builder.HasIndex(x => new { x.ProjectId, x.SprintNumber })
            .IsUnique()
            .HasDatabaseName("IX_Sprints_Project_Number");

        builder.HasIndex(x => new { x.ProjectId, x.Status })
            .HasDatabaseName("IX_Sprints_Project_Status");

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SprintAuditLogEntityConfiguration : IEntityTypeConfiguration<SprintAuditLogEntity>
{
    public void Configure(EntityTypeBuilder<SprintAuditLogEntity> builder)
    {
        builder.ToTable("SprintAuditLogs", EntitySchemas.tasks.ToString());
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChangeType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ChangedBy).HasMaxLength(256);
        builder.Property(x => x.OldValue).HasMaxLength(2000);
        builder.Property(x => x.NewValue).HasMaxLength(2000);
        builder.Property(x => x.Details).HasMaxLength(2000);

        builder.HasOne(x => x.Sprint)
            .WithMany(s => s.AuditLogs)
            .HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SprintItemVersionEntityConfiguration : IEntityTypeConfiguration<SprintItemVersionEntity>
{
    public void Configure(EntityTypeBuilder<SprintItemVersionEntity> builder)
    {
        builder.ToTable("SprintItemVersions", EntitySchemas.tasks.ToString());
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Sprint)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProjectId, x.SprintId })
            .HasDatabaseName("IX_SprintItemVersions_Project_Sprint");

        builder.HasIndex(x => new { x.ItemId, x.SprintId })
            .HasDatabaseName("IX_SprintItemVersions_Item_Sprint");
    }
}
