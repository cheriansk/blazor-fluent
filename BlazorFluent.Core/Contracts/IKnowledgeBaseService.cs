using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Dtos;

namespace BlazorFluent.Core.Contracts;

public interface IKnowledgeBaseService
{
    Task<List<KnowledgeArticleListDto>> GetArticlesAsync(
        string? searchTerm = null,
        KnowledgeArticleStatus? status = null,
        string? labelFilter = null,
        bool? isGlobalOnly = null,
        CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetailDto?> GetArticleByIdAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<Guid> CreateArticleAsync(CreateArticleDto dto, CancellationToken cancellationToken = default);

    Task UpdateArticleAsync(UpdateArticleDto dto, CancellationToken cancellationToken = default);

    Task<bool> ApproveArticleAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<bool> DeleteArticleAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<List<UserDto>> GetAvailableReviewersAsync(CancellationToken cancellationToken = default);

    Task<List<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default);
}

public record UserDto(string Id, string Email, string FullName, UserType UserType);
