namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Architectural interface for entities and DTOs that support optimistic concurrency tracking
/// via PostgreSQL's built-in xmin system column.
/// </summary>
public interface IConcurrency
{
    uint Version { get; set; }
}
