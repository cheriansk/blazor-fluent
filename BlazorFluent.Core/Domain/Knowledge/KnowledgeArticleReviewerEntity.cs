using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Knowledge;

/// <summary>
/// Represents an assigned reviewer for a knowledge article.
/// Extends <see cref="BaseEntity"/>, implements <see cref="IGlobalEntity"/> as a dependent child record governed
/// by the parent article's query filter, and <see cref="IValidationExemptEntity"/>.
/// </summary>
public class KnowledgeArticleReviewerEntity : BaseEntity, IGlobalEntity, IValidationExemptEntity
{
    public Guid ArticleId { get; set; }

    public KnowledgeArticleEntity? Article { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public bool HasApproved { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }
}
