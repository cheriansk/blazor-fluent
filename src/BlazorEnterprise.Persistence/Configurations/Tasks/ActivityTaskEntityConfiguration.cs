using BlazorEnterprise.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class ActivityTaskEntityConfiguration : IEntityTypeConfiguration<ActivityTaskEntity>
{
    public void Configure(EntityTypeBuilder<ActivityTaskEntity> builder)
    {
        builder.ToTable("ActivityTasks", EntitySchemas.tasks.ToString());

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(m => new { m.TenantId, m.Level })
            .HasDatabaseName("IX_ActivityTasks_TenantId_Level");

        builder.HasIndex(m => new { m.TenantId, m.Level, m.Status, m.DueDate })
            .HasDatabaseName("IX_ActivityTasks_Tenant_Level_Status_DueDate");
    }
}
