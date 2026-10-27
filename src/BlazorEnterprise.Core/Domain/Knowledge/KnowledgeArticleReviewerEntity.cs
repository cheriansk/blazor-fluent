using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Identity;

namespace BlazorEnterprise.Core.Domain.Knowledge;

/// <summary>
/// Represents an assigned reviewer for a knowledge article.
/// Extends <see cref="BaseEntity"/>, implements <see cref="IGlobalEntity"/> as a dependent child record governed
/// by the parent article's query filter.
/// Enforces 3NF relational normalization by persisting only <see cref="ArticleId"/> and <see cref="UserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="User"/>.
/// </summary>
public class KnowledgeArticleReviewerEntity : BaseEntity, IGlobalEntity, IHardDeletableEntity
{
    public required Guid ArticleId { get; set; }

    public KnowledgeArticleEntity? Article { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UserEntity"/>.
    /// </summary>
    public required Guid UserEntityId { get; set; }

    public UserEntity? User { get; set; }

    public bool HasApproved { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    // --- [NotMapped] Navigation Forwarders ---
    [NotMapped]
    public string UserId => UserEntityId.ToString();

    [NotMapped]
    public string UserEmail => User?.Email ?? string.Empty;

    [NotMapped]
    public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);
}
