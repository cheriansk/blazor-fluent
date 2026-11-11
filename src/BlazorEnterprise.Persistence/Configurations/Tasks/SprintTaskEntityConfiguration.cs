using BlazorEnterprise.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class SprintTaskEntityConfiguration : IEntityTypeConfiguration<SprintTaskEntity>
{
    public void Configure(EntityTypeBuilder<SprintTaskEntity> builder)
    {
        builder.ToTable("SprintTasks", EntitySchemas.tasks.ToString());

        builder.Property(s => s.SprintId)
            .IsRequired();

        builder.HasOne(s => s.Sprint)
            .WithMany()
            .HasForeignKey(s => s.SprintId)
            .OnDelete(DeleteBehavior.Restrict);

        // Targeted High-Performance Compound Indexes

        builder.HasIndex(s => new { s.TenantId, s.Level })
            .HasDatabaseName("IX_SprintTasks_TenantId_Level");

        builder.HasIndex(s => new { s.TenantId, s.SprintId })
            .HasDatabaseName("IX_SprintTasks_TenantId_SprintId");

        builder.HasIndex(s => new { s.TenantId, s.Level, s.Status, s.DueDate })
            .HasDatabaseName("IX_SprintTasks_Tenant_Level_Status_DueDate");
    }
}
