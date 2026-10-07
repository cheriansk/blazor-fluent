using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.ToTable("Projects", EntitySchemas.tenancy.ToString());

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(p => p.ShortCode)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(p => p.Location)
            .HasMaxLength(250)
            .IsRequired(false);

        builder.Property(p => p.TentativeStartDate)
            .IsRequired(false);

        builder.Property(p => p.TentativeEndDate)
            .IsRequired(false);

        builder.Property(p => p.ScopeSummary)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasDefaultValue(BlazorFluent.Core.DataListTypes.ProjectStatus.New);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.TeamsWebhookUrl)
            .HasMaxLength(2000)
            .IsRequired(false);

        // Soft delete
        builder.Property(p => p.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.DeletedBy).HasMaxLength(256).IsRequired(false);


        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("IX_Projects_TenantId");

        builder.HasIndex(p => new { p.TenantId, p.Name })
            .HasDatabaseName("IX_Projects_TenantId_Name");
    }
}
