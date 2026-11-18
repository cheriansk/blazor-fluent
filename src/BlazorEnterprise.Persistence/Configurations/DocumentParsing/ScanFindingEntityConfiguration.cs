using BlazorEnterprise.Core.Domain.DocumentParsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.DocumentParsing;

/// <summary>
/// EF Core Fluent API configuration for <see cref="ScanFindingEntity"/> (<c>TG010</c>).
/// </summary>
public sealed class ScanFindingEntityConfiguration : IEntityTypeConfiguration<ScanFindingEntity>
{
    public void Configure(EntityTypeBuilder<ScanFindingEntity> builder)
    {
        builder.ToTable("ScanFindings", "app");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CustomerName)
            .HasMaxLength(300);

        builder.Property(x => x.SourceVersion)
            .HasMaxLength(100);

        builder.Property(x => x.TargetVersion)
            .HasMaxLength(100);

        builder.Property(x => x.ProjectManager)
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.Comment)
            .HasMaxLength(4000);

        builder.Property(x => x.DeletedBy)
            .HasMaxLength(256);

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ImportFileId });
        builder.HasIndex(x => x.DocumentParsingResultId);
    }
}
