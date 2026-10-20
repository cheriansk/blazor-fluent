namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Status lifecycle of a knowledge article.
/// </summary>
public enum KnowledgeArticleStatusEnum
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// Audience boundary for a knowledge article.
/// </summary>
public enum KnowledgeArticleScopeEnum
{
    TenantOnly = 1,
    Global = 2
}
