using BlazorEnterprise.Core.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Imports;

public class StagedImportEntityConfiguration : IEntityTypeConfiguration<StagedImportEntity>
{
    public void Configure(EntityTypeBuilder<StagedImportEntity> builder)
    {
        builder.ToTable("StagedImports", "staging");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.ProjectId)
            .IsRequired();

        builder.Property(e => e.ImportFileId)
            .IsRequired();

        builder.Property(e => e.ImportId)
            .IsRequired();

        builder.Property(e => e.FileType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.OriginalFileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.SheetName)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("Default");

        builder.Property(e => e.ChunkIndex)
            .IsRequired();

        builder.Property(e => e.TotalChunks)
            .IsRequired();

        builder.Property(e => e.RowCount)
            .IsRequired();

        builder.Property(e => e.IsProcessed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("Staged");

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.ImportId });
        builder.HasIndex(e => new { e.TenantId, e.ImportFileId, e.IsProcessed, e.ChunkIndex });
        builder.HasIndex(e => new { e.TenantId, e.ProjectId });
    }
}
