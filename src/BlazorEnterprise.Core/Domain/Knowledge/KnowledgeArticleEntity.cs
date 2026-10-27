using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;

namespace BlazorEnterprise.Core.Domain.Knowledge;

/// <summary>
/// Represents a knowledge base article.
/// Extends <see cref="TenantAuditableEntity"/> and <see cref="ISoftDeletableEntity"/>.
/// Can be scoped to a project via <see cref="IProjectScopedEntity"/> or be cross-project/tenant/global.
/// Enforces 3NF relational normalization by persisting only <see cref="AuthorUserEntityId"/> and <see cref="ApprovedByUserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="AuthorUser"/> and <see cref="ApprovedByUser"/>.
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
    [NotMapped]
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
    public required KnowledgeArticleStatusEnum Status { get; set; }

    /// <summary>
    /// Optional project to which the article belongs.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Optional project navigation.
    /// </summary>
    public ProjectEntity? Project { get; set; }

    // --- Author Foreign Key & Navigation ---
    public required Guid AuthorUserEntityId { get; set; }
    public UserEntity? AuthorUser { get; set; }

    // --- Approval Foreign Key & Navigation ---
    public Guid? ApprovedByUserEntityId { get; set; }
    public UserEntity? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    // --- Assigned Reviewers ---
    public ICollection<KnowledgeArticleReviewerEntity> Reviewers { get; set; } = new List<KnowledgeArticleReviewerEntity>();

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- [NotMapped] Navigation Forwarders ---
    [NotMapped]
    public string AuthorUserId => AuthorUserEntityId.ToString();

    [NotMapped]
    public string AuthorUserEmail => AuthorUser?.Email ?? string.Empty;

    [NotMapped]
    public string AuthorUserName => !string.IsNullOrWhiteSpace(AuthorUser?.FullName) ? AuthorUser.FullName : (AuthorUser?.Email ?? string.Empty);

    [NotMapped]
    public string? ApprovedByUserId => ApprovedByUserEntityId?.ToString();

    [NotMapped]
    public string? ApprovedByUserEmail => ApprovedByUser?.Email;

    [NotMapped]
    public string? ApprovedByUserName => !string.IsNullOrWhiteSpace(ApprovedByUser?.FullName) ? ApprovedByUser.FullName : ApprovedByUser?.Email;
}
