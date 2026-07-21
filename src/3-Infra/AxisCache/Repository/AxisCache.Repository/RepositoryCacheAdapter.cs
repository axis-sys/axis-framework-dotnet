using Axis;
using AxisCache.Repository.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace AxisCache.Repository;

/// <summary>
/// A two-tier <see cref="IAxisCache"/>: an in-process L1 (<see cref="IMemoryCache"/>) in front of a durable,
/// cross-instance L2 (<see cref="ICacheEntryStore"/>, the source of truth). Writes go to L2 with their
/// failure PROPAGATED, then warm L1 best-effort; reads serve L1 on a hit and fall back to L2, rehydrating L1
/// for at most <see cref="AxisCacheRepositorySettings.L1Ttl"/> (bounded further by the value's own expiry).
/// Set <c>L1Ttl</c> to zero to bypass L1. Surviving a restart and being visible to every instance come from
/// L2; the tight L1 window is the only staleness across instances.
/// </summary>
internal sealed class RepositoryCacheAdapter(
    IMemoryCache l1,
    ICacheEntryStore store,
    AxisCacheRepositorySettings settings,
    TimeProvider timeProvider
) : IAxisCache
{
    private bool L1Enabled => settings.L1Ttl > TimeSpan.Zero;

    public async Task<AxisResult<T?>> GetAsync<T>(string key)
    {
        var now = timeProvider.GetUtcNow();

        if (L1Enabled && l1.TryGetValue(key, out T? cached))
            return AxisResult.Ok(cached);

        var entry = await store.GetAsync(key, now);
        if (entry.IsFailure)
            return AxisResult.Error<T?>(entry.Errors);

        if (entry.Value is null)
            return AxisResult.Ok<T?>(default);

        var value = AxisCacheSerializer.Deserialize<T>(entry.Value.ValueJson);
        if (value.IsFailure)
            return AxisResult.Error<T?>(value.Errors);

        WarmL1(key, value.Value, entry.Value.ExpiresAt, now);
        return AxisResult.Ok<T?>(value.Value);
    }

    public async Task<AxisResult> SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var now = timeProvider.GetUtcNow();

        var json = AxisCacheSerializer.Serialize(value);
        if (json.IsFailure)
            return AxisResult.Error(json.Errors);

        DateTimeOffset? expiresAt = expiration.HasValue ? now + expiration.Value : null;

        var upsert = await store.UpsertAsync(key, json.Value, expiresAt, now);
        if (upsert.IsFailure)
            return upsert;

        WarmL1(key, value, expiresAt, now);
        return AxisResult.Ok();
    }

    public async Task<AxisResult<T>> GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null)
    {
        var existing = await GetAsync<T>(key);
        if (existing.IsFailure)
            return AxisResult.Error<T>(existing.Errors);

        if (existing.Value is not null)
            return AxisResult.Ok(existing.Value);

        var created = await factory();
        if (created.IsFailure)
            return created;

        var stored = await SetAsync(key, created.Value, expiration);
        return stored.IsFailure ? AxisResult.Error<T>(stored.Errors) : AxisResult.Ok(created.Value);
    }

    public async Task<AxisResult> RemoveAsync(string key)
    {
        var removed = await store.RemoveAsync(key);
        if (L1Enabled)
            l1.Remove(key);
        return removed;
    }

    public async Task<AxisResult<bool>> ExistsAsync(string key)
    {
        var now = timeProvider.GetUtcNow();

        if (L1Enabled && l1.TryGetValue(key, out _))
            return AxisResult.Ok(true);

        var entry = await store.GetAsync(key, now);
        return entry.IsFailure ? AxisResult.Error<bool>(entry.Errors) : AxisResult.Ok(entry.Value is not null);
    }

    // L1 lifetime is the smaller of the configured L1 TTL and the value's own remaining lifetime, so L1
    // never outlives the authoritative L2 entry. An already-expired value is not cached at all.
    private void WarmL1<T>(string key, T value, DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (!L1Enabled)
            return;

        var ttl = settings.L1Ttl;
        if (expiresAt is { } expiry)
        {
            var remaining = expiry - now;
            if (remaining <= TimeSpan.Zero)
                return;
            if (remaining < ttl)
                ttl = remaining;
        }

        l1.Set(key, value, ttl);
    }
}
