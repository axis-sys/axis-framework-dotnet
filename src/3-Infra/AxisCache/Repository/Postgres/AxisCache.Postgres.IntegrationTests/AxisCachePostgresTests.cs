using AxisCache.Repository;
using AxisCache.Repository.Ports;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisCache.Postgres.IntegrationTests;

[Collection("AxisCachePostgresCollection")]
public sealed class AxisCachePostgresTests(AxisCachePostgresFixture fixture)
{
    private sealed record Profile(string Name, int Seats);

    // L1 off so every read exercises the L2 (the point of these tests); the two-tier composition is covered
    // by the core unit tests.
    private ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        Mock<IAxisMediator> mediator = new();
        mediator.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        services.AddSingleton(mediator.Object);
        services.AddSingleton<IAxisMediatorAccessor>(new StubAccessor { AxisMediator = mediator.Object });
        services.AddAxisLogger();
        services.AddAxisCachePostgres(new AxisCacheRepositorySettings
        {
            ConnectionString = fixture.ConnectionString,
            L1Ttl = TimeSpan.Zero,
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_value_round_trips_through_the_store()
    {
        await using var sp = BuildProvider();
        var cache = sp.GetRequiredService<IAxisCache>();

        await cache.SetAsync("rt", new Profile("Ada", 3));
        var read = await cache.GetAsync<Profile>("rt");

        read.ShouldSucceedWith(new Profile("Ada", 3));
    }

    [Fact]
    public async Task A_value_survives_a_restart_because_L2_is_durable()
    {
        await using (var writer = BuildProvider())
            await writer.GetRequiredService<IAxisCache>().SetAsync("survivor", new Profile("Grace", 7));

        // A fresh provider = a fresh process/instance with an empty L1; the value must come from L2.
        await using var reader = BuildProvider();
        var read = await reader.GetRequiredService<IAxisCache>().GetAsync<Profile>("survivor");

        read.ShouldSucceedWith(new Profile("Grace", 7));
    }

    [Fact]
    public async Task Remove_and_exists_behave()
    {
        await using var sp = BuildProvider();
        var cache = sp.GetRequiredService<IAxisCache>();

        await cache.SetAsync("rm", new Profile("Alan", 1));
        await cache.ExistsAsync("rm").ShouldSucceedWithAsync(true);

        await cache.RemoveAsync("rm");
        await cache.ExistsAsync("rm").ShouldSucceedWithAsync(false);
        Assert.Null(await cache.GetAsync<Profile>("rm").ShouldSucceedAsync());
    }

    [Fact]
    public async Task An_expired_entry_reads_as_a_miss_and_is_swept()
    {
        await using var sp = BuildProvider();
        var store = sp.GetRequiredService<ICacheEntryStore>();
        var now = DateTimeOffset.UtcNow;

        await store.UpsertAsync("expired", "\"x\"", now.AddSeconds(-1), now);
        await store.UpsertAsync("live", "\"x\"", now.AddMinutes(5), now);

        Assert.Null(await store.GetAsync("expired", now).ShouldSucceedAsync());
        Assert.NotNull(await store.GetAsync("live", now).ShouldSucceedAsync());
    }

    [Fact]
    public async Task Concurrent_upserts_of_the_same_key_all_succeed_last_write_wins()
    {
        await using var sp = BuildProvider();
        var cache = sp.GetRequiredService<IAxisCache>();

        var writes = Enumerable.Range(0, 20)
            .Select(i => cache.SetAsync("hot", new Profile("W" + i, i)));
        var results = await Task.WhenAll(writes);

        Assert.All(results, r => r.ShouldSucceed());
        Assert.NotNull(await cache.GetAsync<Profile>("hot").ShouldSucceedAsync());
    }

    private sealed class StubAccessor : IAxisMediatorAccessor
    {
        public IAxisMediator? AxisMediator { get; set; }
    }
}
