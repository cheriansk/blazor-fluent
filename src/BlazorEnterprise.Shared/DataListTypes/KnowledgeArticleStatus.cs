using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Shared.DataListTypes;

/// <summary>
/// Status lifecycle of a knowledge article.
/// </summary>
[LifecycleEnum]
public enum KnowledgeArticleStatusEnum
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3
}

public static class KnowledgeArticleStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<KnowledgeArticleStatusEnum, IReadOnlySet<KnowledgeArticleStatusEnum>> AllowedTransitions =
        new Dictionary<KnowledgeArticleStatusEnum, IReadOnlySet<KnowledgeArticleStatusEnum>>
        {
            [KnowledgeArticleStatusEnum.Draft] = new HashSet<KnowledgeArticleStatusEnum> { KnowledgeArticleStatusEnum.PendingReview },
            [KnowledgeArticleStatusEnum.PendingReview] = new HashSet<KnowledgeArticleStatusEnum> { KnowledgeArticleStatusEnum.Approved, KnowledgeArticleStatusEnum.Rejected },
            [KnowledgeArticleStatusEnum.Rejected] = new HashSet<KnowledgeArticleStatusEnum> { KnowledgeArticleStatusEnum.Draft },
            [KnowledgeArticleStatusEnum.Approved] = new HashSet<KnowledgeArticleStatusEnum> { KnowledgeArticleStatusEnum.Draft }
        };
}

/// <summary>
/// Audience boundary for a knowledge article.
/// </summary>
[ClassificationEnum]
public enum KnowledgeArticleScopeEnum
{
    TenantOnly = 1,
    Global = 2
}
