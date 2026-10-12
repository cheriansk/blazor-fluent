namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Enforcing facade for multi-tenant hybrid caching.
/// Automatically namespaces tenant-scoped keys to 't:{tenantId}:{key}' and tags entries with 'tenant:{tenantId}'.
/// Fails closed (throws InvalidOperationException) if a tenant cache call is attempted without an active TenantId context.
/// Provides explicit global operations for host-wide data ('g:{key}').
/// </summary>
public interface ITenantCacheService
{
    /// <summary>
    /// Gets or creates a tenant-scoped cache entry.
    /// Key is automatically namespaced to 't:{tenantId}:{key}'.
    /// Tag is automatically added: 'tenant:{tenantId}'.
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? expiration = null,
        IEnumerable<string>? additionalTags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a global (host-wide) cache entry (e.g. system tenant directory).
    /// Key is automatically namespaced to 'g:{key}'.
    /// Tag is automatically added: 'global'.
    /// </summary>
    ValueTask<T> GetGlobalOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? expiration = null,
        IEnumerable<string>? additionalTags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a tenant-scoped cache entry.
    /// </summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a global (host-wide) cache entry.
    /// </summary>
    ValueTask RemoveGlobalAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges all cache entries for the active tenant or a specified tenant.
    /// Uses tag-based bulk eviction ('tenant:{tenantId}').
    /// </summary>
    ValueTask InvalidateTenantAsync(string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges all cache entries associated with a tenant-scoped tag.
    /// Automatically namespaces tag to 't:{tenantId}:{tag}'.
    /// </summary>
    ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges all cache entries associated with a global tag.
    /// </summary>
    ValueTask RemoveGlobalByTagAsync(string tag, CancellationToken cancellationToken = default);
}
