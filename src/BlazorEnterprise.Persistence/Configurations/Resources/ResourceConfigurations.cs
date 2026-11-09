using BlazorEnterprise.Core.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Resources;

public class ProjectResourceEntityConfiguration : IEntityTypeConfiguration<ProjectResourceEntity>
{
    public void Configure(EntityTypeBuilder<ProjectResourceEntity> builder)
    {
        builder.ToTable("ProjectResources", EntitySchemas.tasks.ToString());
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProjectId, x.UserEntityId })
            .HasDatabaseName("IX_ProjectResources_Project_User");
    }
}

public class ResourceAllocationEntityConfiguration : IEntityTypeConfiguration<ResourceAllocationEntity>
{
    public void Configure(EntityTypeBuilder<ResourceAllocationEntity> builder)
    {
        builder.ToTable("ResourceAllocations", EntitySchemas.tasks.ToString());
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.ProjectResource)
            .WithMany()
            .HasForeignKey(x => x.ProjectResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RevisionHeader)
            .WithMany()
            .HasForeignKey(x => x.RevisionHeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RevisionHeaderId, x.AllocationDate })
            .HasDatabaseName("IX_ResourceAllocations_Plan_Date");

        builder.HasIndex(x => new { x.ProjectResourceId, x.RevisionHeaderId })
            .HasDatabaseName("IX_ResourceAllocations_Resource_Plan");
    }
}

public class ResourceRateEntityConfiguration : IEntityTypeConfiguration<ResourceRateEntity>
{
    public void Configure(EntityTypeBuilder<ResourceRateEntity> builder)
    {
        builder.ToTable("ResourceRates", EntitySchemas.tasks.ToString());
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HourlyRate)
            .HasPrecision(18, 2);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(10);

        builder.HasOne(x => x.ProjectResource)
            .WithMany()
            .HasForeignKey(x => x.ProjectResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RevisionHeader)
            .WithMany()
            .HasForeignKey(x => x.RevisionHeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RevisionHeaderId, x.ProjectResourceId })
            .HasDatabaseName("IX_ResourceRates_Plan_Resource");
    }
}
