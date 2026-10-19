using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Knowledge;

/// <summary>
/// Represents an assigned reviewer for a knowledge article.
/// Extends <see cref="BaseEntity"/>, implements <see cref="IGlobalEntity"/> as a dependent child record governed
/// by the parent article's query filter.
/// </summary>
public class KnowledgeArticleReviewerEntity : BaseEntity, IGlobalEntity, IHardDeletableEntity
{
    public required Guid ArticleId { get; set; }

    public KnowledgeArticleEntity? Article { get; set; }

    public required string UserId { get; set; }

    public required string UserEmail { get; set; }

    public required string UserName { get; set; }

    public bool HasApproved { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }
}
