using BlazorEnterprise.Core.Domain.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Versioning;

public class RevisionHeaderEntityConfiguration : IEntityTypeConfiguration<RevisionHeaderEntity>
{
    public void Configure(EntityTypeBuilder<RevisionHeaderEntity> builder)
    {
        builder.ToTable("RevisionHeaders", EntitySchemas.versioning.ToString(), t =>
        {
            t.HasCheckConstraint(
                "CK_RevisionHeaders_Lineage_Integrity",
                "\"VerNum\" >= 1 AND (\"ParentVerNum\" = -1 OR (\"ParentVerNum\" >= 1 AND \"ParentVerNum\" < \"VerNum\"))");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .HasColumnOrder(0)
            .IsRequired();

        builder.Property(r => r.ScopeLevel)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.FeatureType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.VerNum)
            .IsRequired();

        builder.Property(r => r.ParentVerNum)
            .IsRequired()
            .HasDefaultValue(-1);

        builder.Property(r => r.ParentRevisionHeaderId)
            .IsRequired()
            .HasDefaultValue(Guid.Empty);

        builder.Property(r => r.RevisionName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.CreatorEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.AssignedToEmails)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(r => r.ReviewerEmails)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(r => r.ReviewComments)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(r => r.ApprovedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(r => r.ActivatedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(r => r.CancellationReason)
            .HasMaxLength(2000)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(r => r.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(r => r.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Map ProgramId and ProjectId for scenario scoping
        builder.Property(r => r.ProgramId)
            .IsRequired(false);

        builder.Property(r => r.ProjectId)
            .IsRequired(false);

        // --- Permanent Scenario VerNum Lock Index (Unfiltered) ---
        builder.HasIndex(r => new { r.TenantId, r.ScopeLevel, r.FeatureType, r.ProgramId, r.ProjectId, r.VerNum })
            .IsUnique()
            .HasDatabaseName("IX_RevisionHeaders_Scenario_VerNum_Unique");

        // --- Partial Unique Index: Strictly ONE Active Version per Scenario ---
        builder.HasIndex(r => new { r.TenantId, r.ScopeLevel, r.FeatureType, r.ProgramId, r.ProjectId })
            .HasFilter("\"Status\" = 4 AND \"IsDeleted\" = false")
            .IsUnique()
            .HasDatabaseName("IX_RevisionHeaders_Scenario_SingleActive");
        // Fast lookup index for lineage back-tracing
        builder.HasIndex(r => r.ParentRevisionHeaderId)
            .HasDatabaseName("IX_RevisionHeaders_ParentRevisionHeaderId");

        builder.HasIndex(r => new { r.TenantId, r.Status })
            .HasDatabaseName("IX_RevisionHeaders_Tenant_Status");
    }
}
