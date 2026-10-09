using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents a user's governance role within a specific program (Program Admin or Program Member).
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> to maintain authorization audit history.
/// </summary>
public class ProgramUserRoleEntity : TenantAuditableEntity, ISoftDeletableEntity, IValidationExemptEntity
{
    /// <summary>
    /// ID of the program to which this role assignment belongs.
    /// </summary>
    public Guid ProgramId { get; set; }

    /// <summary>
    /// User identifier string (e.g. email or username).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Governance role: ProgramAdmin or ProgramMember.
    /// </summary>
    public ProgramRole Role { get; set; } = ProgramRole.ProgramMember;

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
