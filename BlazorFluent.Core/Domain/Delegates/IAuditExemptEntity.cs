namespace BlazorFluent.Core.Domain.Delegates;

/// <summary>
/// Marker interface indicating that an entity is exempt from automatic property-diff change auditing.
/// Used for high-volume logs, temporary tables, or the audit record entity itself to prevent recursive auditing loops.
/// </summary>
public interface IAuditExemptEntity
{
}
