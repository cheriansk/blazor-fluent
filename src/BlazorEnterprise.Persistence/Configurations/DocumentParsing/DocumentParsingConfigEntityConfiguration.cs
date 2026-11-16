using BlazorEnterprise.Core.Domain.DocumentParsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.DocumentParsing;

/// <summary>
/// EF Core Fluent API configuration for <see cref="DocumentParsingConfigEntity"/>.
/// Zero database defaults are configured per TG027 fail-closed governance.
/// </summary>
public class DocumentParsingConfigEntityConfiguration : IEntityTypeConfiguration<DocumentParsingConfigEntity>
{
    public void Configure(EntityTypeBuilder<DocumentParsingConfigEntity> builder)
    {
        builder.ToTable("DocumentParsingConfigs", "core");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ModelBlobPath).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.ParsingRulesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.MinConfidenceThreshold).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.DocumentType })
            .HasDatabaseName("IX_DocumentParsingConfigs_TenantId_DocumentType");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_DocumentParsingConfigs_TenantId_IsDeleted");
    }
}
