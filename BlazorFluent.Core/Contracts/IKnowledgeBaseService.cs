using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.DTOs;

namespace BlazorFluent.Core.Contracts;

public interface IKnowledgeBaseService
{
    Task<List<KnowledgeArticleListDto>> GetArticlesAsync(
        string? searchTerm = null,
        KnowledgeArticleStatus? status = null,
        string? labelFilter = null,
        bool? isGlobalOnly = null);

    Task<KnowledgeArticleDetailDto?> GetArticleByIdAsync(Guid articleId);

    Task<Guid> CreateArticleAsync(CreateArticleDto dto);

    Task UpdateArticleAsync(UpdateArticleDto dto);

    Task<bool> ApproveArticleAsync(Guid articleId);

    Task<bool> DeleteArticleAsync(Guid articleId);

    Task<List<UserDto>> GetAvailableReviewersAsync();

    Task<List<string>> GetAllLabelsAsync();
}

public record UserDto(string Id, string Email, string FullName, UserType UserType);
