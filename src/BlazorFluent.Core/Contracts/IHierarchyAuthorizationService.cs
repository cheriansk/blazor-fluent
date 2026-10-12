namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Authoritative Zero Trust security service evaluating hierarchical roles:
/// Tenant Admin -> Program Admin -> Project Admin.
/// Least-privilege fail-closed enforcement: user must hold an explicit role assignment
/// to read or modify any organizational data.
/// </summary>
public interface IHierarchyAuthorizationService
{
    /// <summary>
    /// Checks whether the user is an active Tenant Admin for the specified tenant.
    /// Root Admins automatically return true.
    /// </summary>
    Task<bool> IsTenantAdminAsync(string tenantId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user is an assigned Program Admin for the specified program.
    /// Tenant Admins of the owning tenant automatically return true.
    /// </summary>
    Task<bool> IsProgramAdminAsync(Guid programId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user is an assigned Project Admin for the specified project.
    /// Program Admins of the parent program and Tenant Admins automatically return true.
    /// </summary>
    Task<bool> IsProjectAdminAsync(Guid projectId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Evaluates if the user can view tenant settings (Tenant Admin or Program Admin in read-only mode).
    /// </summary>
    Task<bool> CanViewTenantSettingsAsync(string tenantId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Evaluates if the user can modify tenant settings (strictly Tenant Admin or Root Admin).
    /// </summary>
    Task<bool> CanEditTenantSettingsAsync(string tenantId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Zero Trust gate: verifies the user has active membership in the tenant.
    /// </summary>
    Task<bool> CanAccessTenantAsync(string tenantId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Zero Trust gate: verifies the user has access to a specific program.
    /// </summary>
    Task<bool> CanAccessProgramAsync(Guid programId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Zero Trust gate: verifies the user has access to a specific project.
    /// </summary>
    Task<bool> CanAccessProjectAsync(Guid projectId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the list of program IDs within a tenant that the user is permitted to view.
    /// Tenant Admins receive all programs in the tenant; Program Admins receive only assigned programs.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAccessibleProgramIdsAsync(string tenantId, string userId, CancellationToken ct = default);
}
