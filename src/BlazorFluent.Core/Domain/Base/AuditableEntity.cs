namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// ⚠️ ARCHITECTURAL WARNING: CRITICAL FRAMEWORK BASE CLASS
/// DO NOT MODIFY without architectural review.
/// Provides Level 1 audit properties (Created, CreatedBy, Updated, UpdatedBy).
/// Automatically populated by AuditableEntityInterceptor in BlazorFluent.Persistence.
/// Used for host-wide (global) auditable entities like UserEntity, TenantEntity, and ImpersonationGrantEntity.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    // Inherits Id, Created, CreatedBy, Updated, UpdatedBy, and IsModifiedSinceCreation from BaseEntity.
    // Retained as backward-compatible pass-through for entities inheriting from AuditableEntity.
}
