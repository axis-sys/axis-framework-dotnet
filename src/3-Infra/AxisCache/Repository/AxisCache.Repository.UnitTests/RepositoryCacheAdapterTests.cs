using AxisCache.Repository.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace AxisCache.Repository.UnitTests;

public sealed class RepositoryCacheAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);

    private static RepositoryCacheAdapter Build(out FakeStore store, TimeSpan? l1Ttl = null, DateTimeOffset? now = null)
    {
        store = new FakeStore();
        AxisCacheRepositorySettings settings = new()
        {
            ConnectionString = "unused",
            L1Ttl = l1Ttl ?? TimeSpan.FromSeconds(60),
        };
        return new RepositoryCacheAdapter(
            new MemoryCache(new MemoryCacheOptions()), store, settings, new FixedClock(now ?? Now));
    }

    [Fact]
    public async Task A_set_value_is_served_from_L1_without_touching_L2()
    {
        var cache = Build(out var store);

        await cache.SetAsync("k", "v");
        var read = await cache.GetAsync<string>("k");

        read.ShouldSucceedWith("v");
        Assert.Equal(1, store.UpsertCalls);
        Assert.Equal(0, store.GetCalls); // served from L1
    }

    [Fact]
    public async Task An_L1_miss_reads_L2_once_then_serves_the_rehydrated_L1()
    {
        var cache = Build(out var store);
        store.Seed("k", "\"v\"", expiresAt: null);

        var first = await cache.GetAsync<string>("k");
        var second = await cache.GetAsync<string>("k");

        first.ShouldSucceedWith("v");
        second.ShouldSucceedWith("v");
        Assert.Equal(1, store.GetCalls); // second read hit L1
    }

    [Fact]
    public async Task A_total_miss_returns_null()
    {
        var cache = Build(out _);

        var read = await cache.GetAsync<string>("absent");

        Assert.Null(read.ShouldSucceed());
    }

    [Fact]
    public async Task A_failed_L2_write_is_propagated_and_L1_is_not_warmed()
    {
        var cache = Build(out var store);
        store.FailWrites = true;

        var set = await cache.SetAsync("k", "v");

        set.ShouldFail();
        store.FailWrites = false;
        Assert.Equal(0, store.GetCalls);
        var read = await cache.GetAsync<string>("k"); // L1 empty -> falls to L2 (now empty)
        Assert.Equal(1, store.GetCalls);
        Assert.Null(read.ShouldSucceed());
    }

    [Fact]
    public async Task Remove_clears_L1_and_L2()
    {
        var cache = Build(out var store);
        await cache.SetAsync("k", "v");

        var removed = await cache.RemoveAsync("k");
        var read = await cache.GetAsync<string>("k");

        removed.ShouldSucceed();
        Assert.Null(read.ShouldSucceed());
        Assert.False(store.Contains("k"));
    }

    [Fact]
    public async Task A_zero_L1_ttl_bypasses_memory_and_every_read_hits_L2()
    {
        var cache = Build(out var store, l1Ttl: TimeSpan.Zero);
        await cache.SetAsync("k", "v");

        await cache.GetAsync<string>("k");
        await cache.GetAsync<string>("k");

        Assert.Equal(2, store.GetCalls); // never served from L1
    }

    [Fact]
    public async Task GetOrCreate_runs_the_factory_once_on_miss_then_serves_the_cached_value()
    {
        var cache = Build(out var store);
        var factoryCalls = 0;

        var first = await cache.GetOrCreateAsync("k", () =>
        {
            factoryCalls++;
            return Task.FromResult(AxisResult.Ok("built"));
        });
        var second = await cache.GetOrCreateAsync("k", () =>
        {
            factoryCalls++;
            return Task.FromResult(AxisResult.Ok("built-again"));
        });

        first.ShouldSucceedWith("built");
        second.ShouldSucceedWith("built");
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, store.UpsertCalls);
    }

    [Fact]
    public async Task GetOrCreate_does_not_cache_a_factory_failure()
    {
        var cache = Build(out var store);

        var result = await cache.GetOrCreateAsync(
            "k", () => Task.FromResult(AxisResult.Error<string>(AxisError.BusinessRule("NOPE"))));

        result.ShouldFail();
        Assert.Equal(0, store.UpsertCalls);
        Assert.False(store.Contains("k"));
    }

    [Fact]
    public async Task An_expired_L2_row_reads_as_a_miss()
    {
        var cache = Build(out var store, l1Ttl: TimeSpan.Zero, now: Now);
        store.Seed("k", "\"v\"", expiresAt: Now.AddMinutes(-1)); // already expired at Now

        var read = await cache.GetAsync<string>("k");

        Assert.Null(read.ShouldSucceed());
        Assert.False(store.Contains("k")); // expired row deleted in passing
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeStore : ICacheEntryStore
    {
        private readonly Dictionary<string, CacheEntry> _rows = [];

        public int GetCalls { get; private set; }
        public int UpsertCalls { get; private set; }
        public bool FailWrites { get; set; }

        public void Seed(string key, string json, DateTimeOffset? expiresAt) => _rows[key] = new CacheEntry(json, expiresAt);
        public bool Contains(string key) => _rows.ContainsKey(key);

        public Task<AxisResult<CacheEntry?>> GetAsync(string key, DateTimeOffset nowUtc)
        {
            GetCalls++;
            if (!_rows.TryGetValue(key, out var entry))
                return Task.FromResult(AxisResult.Ok<CacheEntry?>(null));

            if (entry.ExpiresAt is { } expiry && expiry <= nowUtc)
            {
                _rows.Remove(key);
                return Task.FromResult(AxisResult.Ok<CacheEntry?>(null));
            }

            return Task.FromResult(AxisResult.Ok<CacheEntry?>(entry));
        }

        public Task<AxisResult> UpsertAsync(string key, string valueJson, DateTimeOffset? expiresAt, DateTimeOffset nowUtc)
        {
            if (FailWrites)
                return Task.FromResult<AxisResult>(AxisError.InternalServerError("BOOM"));
            UpsertCalls++;
            _rows[key] = new CacheEntry(valueJson, expiresAt);
            return Task.FromResult(AxisResult.Ok());
        }

        public Task<AxisResult> RemoveAsync(string key)
        {
            _rows.Remove(key);
            return Task.FromResult(AxisResult.Ok());
        }

        public Task<AxisResult<int>> DeleteExpiredAsync(DateTimeOffset nowUtc)
        {
            var removed = _rows.Where(kv => kv.Value.ExpiresAt is { } e && e <= nowUtc).Select(kv => kv.Key).ToList().Count;
            return Task.FromResult(AxisResult.Ok(removed));
        }
    }
}
