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
            .HasMaxLength(256)
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

        builder.Property(a => a.AuthorUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.AuthorUserEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.AuthorUserName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.ApprovedByUserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(a => a.ApprovedByUserEmail)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(a => a.ApprovedByUserName)
            .HasMaxLength(256)
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

public class KnowledgeArticleReviewerEntityConfiguration : IEntityTypeConfiguration<KnowledgeArticleReviewerEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleReviewerEntity> builder)
    {
        builder.ToTable("KnowledgeArticleReviewers", EntitySchemas.app.ToString());

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ArticleId)
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.UserEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.UserName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.HasApproved)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.ApprovedAtUtc)
            .IsRequired(false);

        builder.HasIndex(r => r.ArticleId)
            .HasDatabaseName("IX_KnowledgeArticleReviewers_ArticleId");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_KnowledgeArticleReviewers_UserId");
    }
}
