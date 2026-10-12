using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Knowledge;
using BlazorFluent.Core.Dtos;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly INotifyService _notificationService;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(
        AppDbContext context,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        INotifyService notificationService,
        IInputSanitizer inputSanitizer,
        ILogger<KnowledgeBaseService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _notificationService = notificationService;
        _inputSanitizer = inputSanitizer;
        _logger = logger;
    }

    public async Task<List<KnowledgeArticleListDto>> GetArticlesAsync(
        string? searchTerm = null,
        KnowledgeArticleStatus? status = null,
        string? labelFilter = null,
        bool? isGlobalOnly = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated user attempted to access knowledge base articles.");
            return new List<KnowledgeArticleListDto>();
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var query = _context.KnowledgeArticles
            .AsNoTracking()
            .Include(a => a.Reviewers)
            .Where(a => a.IsGlobal || a.TenantId == tenantId)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (isGlobalOnly.HasValue)
        {
            query = query.Where(a => a.IsGlobal == isGlobalOnly.Value);
        }

        if (!string.IsNullOrWhiteSpace(labelFilter))
        {
            var trimmedLabel = labelFilter.Trim().ToLower();
            query = query.Where(a => a.Labels.ToLower().Contains(trimmedLabel));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Content.ToLower().Contains(term) ||
                a.AuthorUserName.ToLower().Contains(term) ||
                a.AuthorUserEmail.ToLower().Contains(term) ||
                a.Labels.ToLower().Contains(term));
        }

        var articles = await query
            .OrderByDescending(a => a.Created)
            .ToListAsync(cancellationToken);

        return articles.Select(a =>
        {
            // Extract snippet from HTML content
            var plainText = System.Text.RegularExpressions.Regex.Replace(a.Content, "<.*?>", string.Empty);
            var snippet = plainText.Length > 160 ? plainText[..160] + "..." : plainText;

            return new KnowledgeArticleListDto(
                a.Id,
                a.Title,
                snippet,
                a.LabelList,
                a.IsGlobal,
                a.Status,
                a.AuthorUserName,
                a.AuthorUserEmail,
                a.Created,
                a.Updated,
                a.ApprovedByUserName,
                a.ApprovedAtUtc,
                a.Reviewers.Count,
                a.Reviewers.Count(r => r.HasApproved)
            );
        }).ToList();
    }

    public async Task<KnowledgeArticleDetailDto?> GetArticleByIdAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated user attempted to access knowledge base article '{ArticleId}'.", articleId);
            return null;
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var a = await _context.KnowledgeArticles
            .AsNoTracking()
            .Include(x => x.Reviewers)
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == articleId && (x.IsGlobal || x.TenantId == tenantId), cancellationToken);

        if (a is null) return null;

        var reviewers = a.Reviewers.Select(r => new ArticleReviewerDto(
            r.UserId,
            r.UserEmail,
            r.UserName,
            r.HasApproved,
            r.ApprovedAtUtc
        )).ToList();

        return new KnowledgeArticleDetailDto(
            a.Id,
            a.Title,
            a.Content,
            a.LabelList,
            a.IsGlobal,
            a.Status,
            a.ProjectId == Guid.Empty ? null : a.ProjectId,
            a.Project?.Name,
            a.AuthorUserId,
            a.AuthorUserName,
            a.AuthorUserEmail,
            a.Created,
            a.Updated,
            a.ApprovedByUserId,
            a.ApprovedByUserName,
            a.ApprovedByUserEmail,
            a.ApprovedAtUtc,
            reviewers
        );
    }

    public async Task<Guid> CreateArticleAsync(CreateArticleDto dto, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Only authenticated users can create knowledge articles.");
        }

        if (dto.Scope is null)
        {
            throw new ArgumentException("Article scope (Tenant Only or Global) must be explicitly specified.", nameof(dto.Scope));
        }

        if (dto.Labels is null || dto.Labels.Count < 1 || dto.Labels.Count > 10)
        {
            throw new ArgumentException("Article must contain between 1 and 10 labels.", nameof(dto.Labels));
        }

        var isGlobal = dto.Scope == KnowledgeArticleScope.Global;
        var labelsString = string.Join(";", dto.Labels.Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct());

        var sanitizedTitle = _inputSanitizer.SanitizePlainText(dto.Title.Trim());
        var sanitizedContent = _inputSanitizer.SanitizeHtml(dto.Content);

        var article = new KnowledgeArticleEntity
        {
            TenantId = _tenantContext.TenantId,
            Title = sanitizedTitle,
            Content = sanitizedContent,
            Labels = labelsString,
            IsGlobal = isGlobal,
            Status = KnowledgeArticleStatus.PendingReview,
            ProjectId = dto.ProjectId ?? Guid.Empty,
            AuthorUserId = _currentUser.UserId ?? string.Empty,
            AuthorUserEmail = _currentUser.Email ?? string.Empty,
            AuthorUserName = !string.IsNullOrWhiteSpace(_currentUser.UserName) ? _currentUser.UserName : (_currentUser.Email ?? "Unknown")
        };

        // Populate reviewers
        if (dto.ReviewerUserIds.Count > 0)
        {
            var reviewerUsers = await _context.Users
                .AsNoTracking()
                .Where(u => dto.ReviewerUserIds.Contains(u.Id.ToString()))
                .ToListAsync(cancellationToken);

            foreach (var user in reviewerUsers)
            {
                article.Reviewers.Add(new KnowledgeArticleReviewerEntity
                {
                    UserId = user.Id.ToString(),
                    UserEmail = user.Email,
                    UserName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email,
                    HasApproved = false
                });
            }
        }

        _context.KnowledgeArticles.Add(article);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Knowledge article '{ArticleId}' created by '{Author}' (Scope: {Scope})",
            article.Id, article.AuthorUserEmail, isGlobal ? "Global" : "TenantOnly");

        // Send in-app notification to assigned reviewers
        foreach (var reviewer in article.Reviewers)
        {
            try
            {
                await _notificationService.SendAsync(new SendNotificationReqDto
                {
                    ProjectId = article.ProjectId,
                    UserId = reviewer.UserId,
                    Category = NotificationCategory.Personal,
                    Severity = NotificationSeverity.Info,
                    Title = "Knowledge Article Review Requested",
                    Message = $"{article.AuthorUserName} requested your review on '{article.Title}'.",
                    LinkUrl = $"/knowledge/{article.Id}"
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification to reviewer '{ReviewerEmail}'", reviewer.UserEmail);
            }
        }

        return article.Id;
    }

    public async Task UpdateArticleAsync(UpdateArticleDto dto, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Only authenticated users can update knowledge articles.");
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var article = await _context.KnowledgeArticles
            .AsTracking()
            .Include(a => a.Reviewers)
            .FirstOrDefaultAsync(a => a.Id == dto.Id && (a.IsGlobal || a.TenantId == tenantId), cancellationToken);

        if (article is null)
        {
            throw new KeyNotFoundException($"Article '{dto.Id}' was not found.");
        }

        article.Title = _inputSanitizer.SanitizePlainText(dto.Title.Trim());
        article.Content = _inputSanitizer.SanitizeHtml(dto.Content);
        if (dto.Scope.HasValue)
        {
            article.IsGlobal = dto.Scope == KnowledgeArticleScope.Global;
        }

        if (dto.Labels.Count > 0)
        {
            article.Labels = string.Join(";", dto.Labels.Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct());
        }

        if (dto.ProjectId.HasValue)
        {
            article.ProjectId = dto.ProjectId.Value;
        }

        // Reset to PendingReview if content changed significantly
        if (article.Status == KnowledgeArticleStatus.Approved)
        {
            article.Status = KnowledgeArticleStatus.PendingReview;
            article.ApprovedAtUtc = null;
            article.ApprovedByUserId = null;
            article.ApprovedByUserName = null;
            article.ApprovedByUserEmail = null;
            foreach (var r in article.Reviewers)
            {
                r.HasApproved = false;
                r.ApprovedAtUtc = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ApproveArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var article = await _context.KnowledgeArticles
            .AsTracking()
            .Include(a => a.Reviewers)
            .FirstOrDefaultAsync(a => a.Id == articleId && (a.IsGlobal || a.TenantId == tenantId), cancellationToken);

        if (article is null) return false;

        var currentUserId = _currentUser.UserId;
        var reviewer = article.Reviewers.FirstOrDefault(r => r.UserId == currentUserId);

        var isHostOrAdmin = _currentUser.IsRootAdmin;
        if (reviewer is null && !isHostOrAdmin)
        {
            _logger.LogWarning("User '{UserId}' attempted to approve article '{ArticleId}' without reviewer assignment", currentUserId, articleId);
            return false;
        }

        var now = DateTime.UtcNow;
        if (reviewer is not null)
        {
            reviewer.HasApproved = true;
            reviewer.ApprovedAtUtc = now;
        }

        // 1-approval workflow: Any 1 reviewer approval marks article as Approved
        article.Status = KnowledgeArticleStatus.Approved;
        article.ApprovedByUserId = currentUserId;
        article.ApprovedByUserEmail = _currentUser.Email;
        article.ApprovedByUserName = !string.IsNullOrWhiteSpace(_currentUser.UserName) ? _currentUser.UserName : _currentUser.Email;
        article.ApprovedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Knowledge article '{ArticleId}' approved by '{Approver}'", articleId, article.ApprovedByUserEmail);

        // Notify article author
        try
        {
            await _notificationService.SendAsync(new SendNotificationReqDto
            {
                ProjectId = article.ProjectId,
                UserId = article.AuthorUserId,
                Category = NotificationCategory.Personal,
                Severity = NotificationSeverity.Info,
                Title = "Knowledge Article Approved",
                Message = $"Your article '{article.Title}' was approved by {article.ApprovedByUserName}.",
                LinkUrl = $"/knowledge/{article.Id}"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send approval notification to author '{AuthorUserId}'", article.AuthorUserId);
        }

        return true;
    }

    public async Task<bool> DeleteArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var article = await _context.KnowledgeArticles
            .AsTracking()
            .FirstOrDefaultAsync(a => a.Id == articleId && (a.IsGlobal || a.TenantId == tenantId), cancellationToken);
        if (article is null) return false;

        article.IsDeleted = true;
        article.DeletedAtUtc = DateTime.UtcNow;
        article.DeletedBy = _currentUser.Email;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<UserDto>> GetAvailableReviewersAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return new List<UserDto>();
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted && u.UserType == UserType.CompanyUser)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        return users.Select(u => new UserDto(
            u.Id.ToString(),
            u.Email,
            !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.Email,
            u.UserType
        )).ToList();
    }

    public async Task<List<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return new List<string>();
        }

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var labelStrings = await _context.KnowledgeArticles
            .AsNoTracking()
            .Where(a => a.IsGlobal || a.TenantId == tenantId)
            .Select(a => a.Labels)
            .ToListAsync(cancellationToken);

        var labels = labelStrings
            .SelectMany(s => s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();

        return labels;
    }
}
