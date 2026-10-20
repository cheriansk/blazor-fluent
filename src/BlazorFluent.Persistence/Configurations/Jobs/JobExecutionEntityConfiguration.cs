using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Jobs;

public class JobExecutionEntityConfiguration : IEntityTypeConfiguration<JobExecutionEntity>
{
    public void Configure(EntityTypeBuilder<JobExecutionEntity> builder)
    {
        builder.ToTable("JobExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ParentExecutionId)
            .IsRequired(false);

        builder.Property(e => e.JobName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TriggerSource)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.AttemptCount)
            .IsRequired();

        builder.Property(e => e.MaxRetries)
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .IsRequired(false);

        builder.Property(e => e.ErrorMessage)
            .IsRequired(false);

        builder.Property(e => e.StartedAt)
            .IsRequired(false);

        builder.Property(e => e.CompletedAt)
            .IsRequired(false);

        builder.Property(e => e.DurationMs)
            .IsRequired(false);

        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);
    }
}
