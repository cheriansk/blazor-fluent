using BlazorEnterprise.Core.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Knowledge;

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

        builder.HasOne(r => r.Article)
            .WithMany(a => a.Reviewers)
            .HasForeignKey(r => r.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.ArticleId, r.UserEntityId })
            .IsUnique()
            .HasDatabaseName("IX_KnowledgeArticleReviewers_Article_User");
    }
}
