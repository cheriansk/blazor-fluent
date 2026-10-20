using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents a user's governance role within a specific program (Program Admin or Program Member).
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> to maintain authorization audit history.
/// Enforces 3NF relational normalization by persisting only <see cref="ProgramId"/> and <see cref="UserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="User"/>.
/// </summary>
public class ProgramUserRoleEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    /// <summary>
    /// ID of the program to which this role assignment belongs.
    /// </summary>
    public required Guid ProgramId { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UserEntity"/>.
    /// </summary>
    public required Guid UserEntityId { get; set; }

    /// <summary>
    /// Governance role: ProgramAdmin or ProgramMember.
    /// </summary>
    public required ProgramRole Role { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation ---
    public ProgramEntity? Program { get; set; }
    public UserEntity? User { get; set; }

    // --- [NotMapped] Navigation Forwarders ---
    [NotMapped]
    public string UserId => UserEntityId.ToString();

    [NotMapped]
    public string UserEmail => User?.Email ?? string.Empty;

    [NotMapped]
    public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);
}
