using BlazorFluent.Core.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Notifications;

public class NotificationEntityConfiguration : IEntityTypeConfiguration<NotificationEntity>
{
    public void Configure(EntityTypeBuilder<NotificationEntity> builder)
    {
        builder.ToTable("Notifications", EntitySchemas.app.ToString());

        builder.HasKey(n => n.Id);

        builder.Property(n => n.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(n => n.ProjectId)
            .IsRequired();

        builder.Property(n => n.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(n => n.Category)
            .IsRequired();

        builder.Property(n => n.Severity)
            .IsRequired();

        builder.Property(n => n.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(n => n.LinkUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.ReadAtUtc)
            .IsRequired(false);

        builder.Property(n => n.SentToTeams)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.TeamsSentAtUtc)
            .IsRequired(false);

        builder.Property(n => n.SentToMailbox)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.MailboxSentAtUtc)
            .IsRequired(false);

        builder.Property(n => n.MetadataJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        // Soft delete properties
        builder.Property(n => n.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(n => n.DeletedAtUtc)
            .IsRequired(false);

        // Indexes for fast, isolated queries
        builder.HasIndex(n => n.TenantId)
            .HasDatabaseName("IX_Notifications_TenantId");

        builder.HasIndex(n => new { n.TenantId, n.UserId, n.IsRead })
            .HasDatabaseName("IX_Notifications_Tenant_User_IsRead")
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(n => new { n.TenantId, n.ProjectId, n.Category })
            .HasDatabaseName("IX_Notifications_Tenant_Project_Category")
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(n => n.Created)
            .HasDatabaseName("IX_Notifications_Created");
    }
}
