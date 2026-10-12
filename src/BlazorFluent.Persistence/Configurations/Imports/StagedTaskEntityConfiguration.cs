using BlazorFluent.Core.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Imports;

public class StagedTaskEntityConfiguration : IEntityTypeConfiguration<StagedTaskEntity>
{
    public void Configure(EntityTypeBuilder<StagedTaskEntity> builder)
    {
        builder.ToTable("StagedTasks", "staging");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.ProjectId)
            .IsRequired();

        builder.Property(e => e.ImportFileId)
            .IsRequired();

        builder.Property(e => e.ImportId)
            .IsRequired();

        builder.Property(e => e.SheetName)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("Tasks");

        builder.Property(e => e.RowIndex)
            .IsRequired();

        builder.Property(e => e.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(e => e.Priority)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("Medium");

        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("Open");

        builder.Property(e => e.DueDate)
            .IsRequired(false);

        builder.Property(e => e.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(e => e.Labels)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.ValidationStatus)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("Valid");

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000)
            .IsRequired(false);

        // Foreign index
        builder.HasIndex(e => new { e.TenantId, e.ImportId });
        builder.HasIndex(e => new { e.TenantId, e.ImportFileId, e.ValidationStatus });
        builder.HasIndex(e => new { e.TenantId, e.ProjectId });
    }
}
