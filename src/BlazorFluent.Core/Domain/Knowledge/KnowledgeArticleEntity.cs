using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Domain.Knowledge;

/// <summary>
/// Represents a knowledge base article.
/// Extends <see cref="TenantAuditableEntity"/> and <see cref="ISoftDeletableEntity"/>.
/// Can be scoped to a project via <see cref="IProjectScopedEntity"/> or be cross-project/tenant/global.
/// </summary>
public class KnowledgeArticleEntity : TenantAuditableEntity, ISoftDeletableEntity, IProjectScopedEntity
{
    /// <summary>
    /// Concise headline title of the article. Required. Max 100 characters.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Rich text HTML content and embedded base64/URL images. Required. Max 20,000 characters.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Semicolon-delimited list of 1 to 10 labels/tags (e.g. 'Architecture;Frontend;API').
    /// </summary>
    public string? Labels { get; set; }

    /// <summary>
    /// Helper to access parsed labels.
    /// </summary>
    public List<string> LabelList => string.IsNullOrWhiteSpace(Labels)
        ? new List<string>()
        : Labels.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>
    /// Indicates whether the article is visible globally across all tenants.
    /// If false, visible only within the author's tenant.
    /// </summary>
    public bool IsGlobal { get; set; }

    /// <summary>
    /// Current approval lifecycle status.
    /// </summary>
    public required KnowledgeArticleStatus Status { get; set; }

    /// <summary>
    /// Optional project to which the article belongs.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Optional project navigation.
    /// </summary>
    public ProjectEntity? Project { get; set; }

    // --- Author Details ---
    public required string AuthorUserId { get; set; }
    public required string AuthorUserEmail { get; set; }
    public required string AuthorUserName { get; set; }

    // --- Approval Details ---
    public string? ApprovedByUserId { get; set; }
    public string? ApprovedByUserEmail { get; set; }
    public string? ApprovedByUserName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    // --- Assigned Reviewers ---
    public ICollection<KnowledgeArticleReviewerEntity> Reviewers { get; set; } = new List<KnowledgeArticleReviewerEntity>();

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
