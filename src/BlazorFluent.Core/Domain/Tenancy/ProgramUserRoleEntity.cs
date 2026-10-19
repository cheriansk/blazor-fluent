using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents a user's governance role within a specific program (Program Admin or Program Member).
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> to maintain authorization audit history.
/// </summary>
public class ProgramUserRoleEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    /// <summary>
    /// ID of the program to which this role assignment belongs.
    /// </summary>
    public required Guid ProgramId { get; set; }

    /// <summary>
    /// User identifier string (e.g. email or username).
    /// </summary>
    public required string UserId { get; set; }

    public required string UserEmail { get; set; }

    public required string UserName { get; set; }

    /// <summary>
    /// Governance role: ProgramAdmin or ProgramMember.
    /// </summary>
    public required ProgramRole Role { get; set; }

    /// <summary>
    /// Optional foreign key to UserEntity.
    /// </summary>
    public Guid? UserEntityId { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation ---
    public ProgramEntity? Program { get; set; }
    public UserEntity? User { get; set; }
}
