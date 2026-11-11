using BlazorEnterprise.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class MiscTaskEntityConfiguration : IEntityTypeConfiguration<MiscTaskEntity>
{
    public void Configure(EntityTypeBuilder<MiscTaskEntity> builder)
    {
        builder.ToTable("MiscTasks", EntitySchemas.tasks.ToString());

        // Targeted High-Performance Compound Indexes

        builder.HasIndex(m => new { m.TenantId, m.Level })
            .HasDatabaseName("IX_MiscTasks_TenantId_Level");

        builder.HasIndex(m => new { m.TenantId, m.Level, m.Status, m.DueDate })
            .HasDatabaseName("IX_MiscTasks_Tenant_Level_Status_DueDate");
    }
}
