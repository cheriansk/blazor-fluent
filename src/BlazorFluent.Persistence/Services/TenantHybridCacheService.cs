using BlazorFluent.Core.Contracts;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Production-grade implementation of <see cref="ITenantCacheService"/> utilizing .NET 10 <see cref="HybridCache"/>.
/// Enforces fail-closed multi-tenant key namespacing, tenant tagging, and cache invalidation.
/// </summary>
public sealed class TenantHybridCacheService : ITenantCacheService
{
    private readonly HybridCache _hybridCache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantHybridCacheService> _logger;

    public TenantHybridCacheService(
        HybridCache hybridCache,
        ITenantContext tenantContext,
        ILogger<TenantHybridCacheService> logger)
    {
        _hybridCache = hybridCache;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? expiration = null,
        IEnumerable<string>? additionalTags = null,
        CancellationToken cancellationToken = default)
    {
        var namespacedKey = BuildTenantKey(key);
        var effectiveTenantId = _tenantContext.TenantId!.Trim().ToLowerInvariant();

        var tags = new List<string> { $"tenant:{effectiveTenantId}" };
        if (additionalTags != null)
        {
            tags.AddRange(additionalTags.Select(t => $"t:{effectiveTenantId}:{t.Trim()}"));
        }

        var entryOptions = expiration.HasValue
            ? new HybridCacheEntryOptions { Expiration = expiration.Value }
            : null;

        return await _hybridCache.GetOrCreateAsync(
            namespacedKey,
            async token =>
            {
                _logger.LogDebug("Cache miss (invoking factory): Key={Key}, TenantId={TenantId}", namespacedKey, effectiveTenantId);
                return await factory(token);
            },
            entryOptions,
            tags,
            cancellationToken);
    }

    public async ValueTask<T> GetGlobalOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? expiration = null,
        IEnumerable<string>? additionalTags = null,
        CancellationToken cancellationToken = default)
    {
        var namespacedKey = BuildGlobalKey(key);
        var tags = new List<string> { "global" };
        if (additionalTags != null)
        {
            tags.AddRange(additionalTags.Select(t => t.Trim()));
        }

        var entryOptions = expiration.HasValue
            ? new HybridCacheEntryOptions { Expiration = expiration.Value }
            : null;

        return await _hybridCache.GetOrCreateAsync(
            namespacedKey,
            async token =>
            {
                _logger.LogDebug("Global cache miss (invoking factory): Key={Key}", namespacedKey);
                return await factory(token);
            },
            entryOptions,
            tags,
            cancellationToken);
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var namespacedKey = BuildTenantKey(key);
        await _hybridCache.RemoveAsync(namespacedKey, cancellationToken);
        _logger.LogInformation("Tenant cache entry removed: Key={Key}", namespacedKey);
    }

    public async ValueTask RemoveGlobalAsync(string key, CancellationToken cancellationToken = default)
    {
        var namespacedKey = BuildGlobalKey(key);
        await _hybridCache.RemoveAsync(namespacedKey, cancellationToken);
        _logger.LogInformation("Global cache entry removed: Key={Key}", namespacedKey);
    }

    public async ValueTask InvalidateTenantAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = string.IsNullOrWhiteSpace(tenantId) ? _tenantContext.TenantId : tenantId;
        if (string.IsNullOrWhiteSpace(effectiveTenantId))
        {
            throw new InvalidOperationException("Cannot invalidate tenant cache: No tenant ID was provided or active in context.");
        }

        var normalizedTenantId = effectiveTenantId.Trim().ToLowerInvariant();
        var tag = $"tenant:{normalizedTenantId}";

        await _hybridCache.RemoveByTagAsync(tag, cancellationToken);
        _logger.LogInformation("Tenant cache purged via tag: TenantId={TenantId}, Tag={Tag}", normalizedTenantId, tag);
    }

    public async ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("Security violation: Attempted to remove tenant tag without an active TenantId context.");
        }

        var namespacedTag = $"t:{tenantId.Trim().ToLowerInvariant()}:{tag.Trim()}";
        await _hybridCache.RemoveByTagAsync(namespacedTag, cancellationToken);
        _logger.LogInformation("Tenant cache tag purged: Tag={Tag}", namespacedTag);
    }

    public async ValueTask RemoveGlobalByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await _hybridCache.RemoveByTagAsync(tag.Trim(), cancellationToken);
        _logger.LogInformation("Global cache tag purged: Tag={Tag}", tag);
    }

    private string BuildTenantKey(string rawKey)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogError("Security violation: Attempted to perform a tenant-scoped cache operation without an active TenantId.");
            throw new InvalidOperationException(
                "Security violation: Attempted to perform a tenant-scoped cache operation without an active TenantId context.");
        }

        return $"t:{tenantId.Trim().ToLowerInvariant()}:{rawKey.Trim()}";
    }

    private static string BuildGlobalKey(string rawKey) => $"g:{rawKey.Trim()}";
}
