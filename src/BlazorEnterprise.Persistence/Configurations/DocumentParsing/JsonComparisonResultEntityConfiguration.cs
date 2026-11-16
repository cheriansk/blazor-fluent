using BlazorEnterprise.Core.Domain.DocumentParsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.DocumentParsing;

/// <summary>
/// EF Core Fluent API configuration for <see cref="JsonComparisonResultEntity"/>.
/// Zero database defaults are configured per TG027 fail-closed governance.
/// </summary>
public class JsonComparisonResultEntityConfiguration : IEntityTypeConfiguration<JsonComparisonResultEntity>
{
    public void Configure(EntityTypeBuilder<JsonComparisonResultEntity> builder)
    {
        builder.ToTable("JsonComparisonResults", "core");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.SourceFileId).IsRequired();
        builder.Property(e => e.TargetFileId).IsRequired();
        builder.Property(e => e.DiffResultJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.SummaryMetricsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();

        builder.HasOne(e => e.SourceFile)
            .WithMany()
            .HasForeignKey(e => e.SourceFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TargetFile)
            .WithMany()
            .HasForeignKey(e => e.TargetFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.ProjectId })
            .HasDatabaseName("IX_JsonComparisonResults_TenantId_ProjectId");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_JsonComparisonResults_TenantId_IsDeleted");
    }
}
