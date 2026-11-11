using BlazorEnterprise.Core.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Knowledge;

public class KnowledgeArticleEntityConfiguration : IEntityTypeConfiguration<KnowledgeArticleEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleEntity> builder)
    {
        builder.ToTable("KnowledgeArticles", EntitySchemas.app.ToString());

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .HasColumnOrder(0)
            .IsRequired();

        builder.Property(a => a.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Content)
            .HasMaxLength(20000)
            .IsRequired();

        builder.Property(a => a.Labels)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(a => a.IsGlobal)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.AuthorUserEntityId)
            .IsRequired();

        builder.Property(a => a.ApprovedByUserEntityId)
            .IsRequired(false);

        builder.Property(a => a.ApprovedAtUtc)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(a => a.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Ignore [NotMapped] edge relations and navigation
        builder.Property(a => a.ProjectId).IsRequired(false);
        builder.Ignore(a => a.Project);

        // Relationship: Article has many Reviewers
        builder.HasMany(a => a.Reviewers)
            .WithOne(r => r.Article)
            .HasForeignKey(r => r.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Author relationship
        builder.HasOne(a => a.AuthorUser)
            .WithMany()
            .HasForeignKey(a => a.AuthorUserEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Approver relationship
        builder.HasOne(a => a.ApprovedByUser)
            .WithMany()
            .HasForeignKey(a => a.ApprovedByUserEntityId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("IX_KnowledgeArticles_TenantId");

        builder.HasIndex(a => a.IsGlobal)
            .HasDatabaseName("IX_KnowledgeArticles_IsGlobal");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_KnowledgeArticles_Status");

        builder.HasIndex(a => a.IsDeleted)
            .HasDatabaseName("IX_KnowledgeArticles_IsDeleted");
    }
}
