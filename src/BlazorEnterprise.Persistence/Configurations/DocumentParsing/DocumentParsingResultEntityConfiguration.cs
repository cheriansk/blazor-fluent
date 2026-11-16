using BlazorEnterprise.Core.Domain.DocumentParsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.DocumentParsing;

/// <summary>
/// EF Core Fluent API configuration for <see cref="DocumentParsingResultEntity"/>.
/// Zero database defaults are configured per TG027 fail-closed governance.
/// </summary>
public class DocumentParsingResultEntityConfiguration : IEntityTypeConfiguration<DocumentParsingResultEntity>
{
    public void Configure(EntityTypeBuilder<DocumentParsingResultEntity> builder)
    {
        builder.ToTable("DocumentParsingResults", "core");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ImportFileId).IsRequired();
        builder.Property(e => e.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExtractedDataJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.ConfidenceScore).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.FinalizedAtUtc).IsRequired(false);
        builder.Property(e => e.FinalizedBy).HasMaxLength(256).IsRequired(false);
        builder.Property(e => e.IsDeleted).IsRequired();

        builder.HasOne(e => e.ImportFile)
            .WithMany()
            .HasForeignKey(e => e.ImportFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.ProjectId })
            .HasDatabaseName("IX_DocumentParsingResults_TenantId_ProjectId");

        builder.HasIndex(e => new { e.TenantId, e.DocumentType })
            .HasDatabaseName("IX_DocumentParsingResults_TenantId_DocumentType");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_DocumentParsingResults_TenantId_IsDeleted");
    }
}
