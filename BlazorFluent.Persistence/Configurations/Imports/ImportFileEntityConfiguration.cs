using BlazorFluent.Core.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Imports;

public class ImportFileEntityConfiguration : IEntityTypeConfiguration<ImportFileEntity>
{
    public void Configure(EntityTypeBuilder<ImportFileEntity> builder)
    {
        builder.ToTable("ImportFiles", "imports");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.ProjectId)
            .IsRequired();

        builder.Property(e => e.ImportId)
            .IsRequired();

        builder.Property(e => e.StorageFolder)
            .HasMaxLength(1024)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(e => e.StepStage)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("None");

        builder.Property(e => e.FileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(e => e.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(e => e.BlobUri)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(e => e.ContentType)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("application/octet-stream");

        builder.Property(e => e.FileSizeBytes)
            .IsRequired();

        builder.Property(e => e.Sha256Checksum)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.FileType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.TotalRowsCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ValidRowsCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ErrorRowsCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ValidationErrorsJson)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(e => e.ProcessedAtUtc)
            .IsRequired(false);

        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(e => e.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Indexes for performance and query isolation
        builder.HasIndex(e => new { e.TenantId, e.ImportId });
        builder.HasIndex(e => new { e.TenantId, e.ProjectId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.Sha256Checksum });
    }
}
