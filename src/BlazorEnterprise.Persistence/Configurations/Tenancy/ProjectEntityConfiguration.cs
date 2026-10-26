using BlazorEnterprise.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tenancy;

public class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.ToTable("Projects", EntitySchemas.tenancy.ToString());

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
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

        builder.Property(p => p.StartDate)
            .IsRequired();

        builder.Property(p => p.DueDate)
            .IsRequired();

        builder.Property(p => p.RevisedDueDate)
            .IsRequired(false);

        builder.Property(p => p.EndDate)
            .IsRequired(false);

        builder.Property(p => p.ActualStartDate)
            .IsRequired(false);

        builder.Property(p => p.ActualEndDate)
            .IsRequired(false);

        builder.Property(p => p.ScopeSummary)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasDefaultValue(ProjectStatusEnum.New);

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

        builder.HasIndex(p => new { p.TenantId, p.ShortCode })
            .HasDatabaseName("IX_Projects_TenantId_ShortCode")
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"ShortCode\" IS NOT NULL AND \"ShortCode\" <> ''");

        builder.HasIndex(p => p.ProgramId)
            .HasDatabaseName("IX_Projects_ProgramId");

        builder.HasOne(p => p.TenantEntity)
            .WithMany(t => t.Projects)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Program)
            .WithMany(pr => pr.Projects)
            .HasForeignKey(p => p.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
