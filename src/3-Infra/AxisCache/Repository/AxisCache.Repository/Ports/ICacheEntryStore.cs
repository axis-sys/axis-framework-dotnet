using Axis;

namespace AxisCache.Repository.Ports;

/// <summary>The L2 (SQL) tier: the durable, cross-instance source of truth behind the two-tier adapter.</summary>
public interface ICacheEntryStore
{
    /// <summary>
    /// Returns the live entry for <paramref name="key"/>, or <c>null</c> when absent or expired at
    /// <paramref name="nowUtc"/> (an expired row is deleted in passing). Never throws.
    /// </summary>
    Task<AxisResult<CacheEntry?>> GetAsync(string key, DateTimeOffset nowUtc);

    /// <summary>Inserts or replaces the entry. A failure here is propagated — L2 is the source of truth.</summary>
    Task<AxisResult> UpsertAsync(string key, string valueJson, DateTimeOffset? expiresAt, DateTimeOffset nowUtc);

    Task<AxisResult> RemoveAsync(string key);

    /// <summary>Deletes every row whose expiry has passed <paramref name="nowUtc"/>. Used by the periodic sweep worker.</summary>
    Task<AxisResult<int>> DeleteExpiredAsync(DateTimeOffset nowUtc);
}

/// <summary>An L2 row: the serialized value and its optional absolute expiry.</summary>
public sealed record CacheEntry(string ValueJson, DateTimeOffset? ExpiresAt);
