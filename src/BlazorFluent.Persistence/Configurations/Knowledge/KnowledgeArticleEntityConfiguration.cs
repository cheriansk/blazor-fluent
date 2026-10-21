using BlazorFluent.Core.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Knowledge;

public class KnowledgeArticleEntityConfiguration : IEntityTypeConfiguration<KnowledgeArticleEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleEntity> builder)
    {
        builder.ToTable("KnowledgeArticles", EntitySchemas.app.ToString());

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
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

        builder.Property(a => a.ProjectId)
            .IsRequired(true);

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

        // Relationship: Article has many Reviewers
        builder.HasMany(a => a.Reviewers)
            .WithOne(r => r.Article)
            .HasForeignKey(r => r.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional relationship to Project
        builder.HasOne(a => a.Project)
            .WithMany()
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

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

        builder.HasIndex(a => new { a.TenantId, a.ProjectId })
            .HasDatabaseName("IX_KnowledgeArticles_TenantId_ProjectId");

        builder.HasIndex(a => a.IsGlobal)
            .HasDatabaseName("IX_KnowledgeArticles_IsGlobal");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_KnowledgeArticles_Status");

        builder.HasIndex(a => a.IsDeleted)
            .HasDatabaseName("IX_KnowledgeArticles_IsDeleted");
    }
}

public class KnowledgeArticleReviewerEntityConfiguration : IEntityTypeConfiguration<KnowledgeArticleReviewerEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleReviewerEntity> builder)
    {
        builder.ToTable("KnowledgeArticleReviewers", EntitySchemas.app.ToString());

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ArticleId)
            .IsRequired();

        builder.Property(r => r.UserEntityId)
            .IsRequired();

        builder.Property(r => r.HasApproved)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.ApprovedAtUtc)
            .IsRequired(false);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ArticleId)
            .HasDatabaseName("IX_KnowledgeArticleReviewers_ArticleId");

        builder.HasIndex(r => r.UserEntityId)
            .HasDatabaseName("IX_KnowledgeArticleReviewers_UserEntityId");
    }
}
