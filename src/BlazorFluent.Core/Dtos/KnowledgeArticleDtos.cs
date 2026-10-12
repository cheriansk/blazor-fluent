using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Dtos;

public record KnowledgeArticleListDto(
    Guid Id,
    string Title,
    string Snippet,
    List<string> Labels,
    bool IsGlobal,
    KnowledgeArticleStatus Status,
    string AuthorUserName,
    string AuthorUserEmail,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? ApprovedByUserName,
    DateTime? ApprovedAtUtc,
    int ReviewerCount,
    int ApprovedReviewerCount
);

public record ArticleReviewerDto(
    string UserId,
    string UserEmail,
    string UserName,
    bool HasApproved,
    DateTime? ApprovedAtUtc
);

public record KnowledgeArticleDetailDto(
    Guid Id,
    string Title,
    string Content,
    List<string> Labels,
    bool IsGlobal,
    KnowledgeArticleStatus Status,
    Guid? ProjectId,
    string? ProjectName,
    string AuthorUserId,
    string AuthorUserName,
    string AuthorUserEmail,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? ApprovedByUserId,
    string? ApprovedByUserName,
    string? ApprovedByUserEmail,
    DateTime? ApprovedAtUtc,
    List<ArticleReviewerDto> Reviewers
);

public record CreateArticleDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public KnowledgeArticleScope? Scope { get; set; } // Nullable: no default selected!
    public List<string> Labels { get; set; } = new(); // Starts empty: no automatic labels!
    public Guid? ProjectId { get; set; }
    public List<string> ReviewerUserIds { get; set; } = new();
}

public record UpdateArticleDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public KnowledgeArticleScope? Scope { get; set; }
    public List<string> Labels { get; set; } = new();
    public Guid? ProjectId { get; set; }
    public List<string> ReviewerUserIds { get; set; } = new();
}
