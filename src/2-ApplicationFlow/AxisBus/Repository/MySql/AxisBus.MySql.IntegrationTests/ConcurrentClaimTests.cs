using AxisBus.Repository.Ports;
using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace AxisBus.MySql.IntegrationTests;

/// <summary>
/// Two separate provider instances (simulating two app instances/pods) racing over the same batch of pending
/// rows. Each row is a singleton partition (distinct ordering key), and the dispatch store claims each head
/// under a lease guard, so every row is delivered — and its handler invoked — by exactly one of the two
/// dispatchers, and the table is fully drained (delivery = deletion).
/// </summary>
[Collection("AxisBusMySqlCollection")]
public class ConcurrentClaimTests(AxisBusMySqlFixture fixture)
{
    private sealed record ConcurrencyTestEvent(string Marker) : IAxisEvent;

    private sealed class CountingHandler(ConcurrentDictionary<string, int> counts) : IAxisEventHandler<ConcurrencyTestEvent>
    {
        public Task<AxisResult> HandleAsync(ConcurrencyTestEvent @event)
        {
            counts.AddOrUpdate(@event.Marker, 1, (_, c) => c + 1);
            return Task.FromResult(AxisResult.Ok());
        }
    }

    [Fact]
    public async Task TwoDispatchersRacingTheSameBatch_EachRowIsDeliveredExactlyOnceAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        const int rowCount = 25;
        ConcurrentDictionary<string, int> counts = new();

        await using var instanceA = fixture.BuildProvider(
            batchSize: rowCount,
            configureServices: services => services.AddScoped<IAxisEventHandler<ConcurrencyTestEvent>>(_ => new CountingHandler(counts)));
        await using var instanceB = fixture.BuildProvider(
            batchSize: rowCount,
            configureServices: services => services.AddScoped<IAxisEventHandler<ConcurrencyTestEvent>>(_ => new CountingHandler(counts)));

        var markers = Enumerable.Range(0, rowCount).Select(i => $"row-{i}-{Guid.NewGuid():N}").ToArray();
        await instanceA.PublishAllAndDrainAsync(markers.Select(m => new ConcurrencyTestEvent(m)), ct);

        var dispatcherA = instanceA.GetRequiredService<IBusDispatcher>();
        var dispatcherB = instanceB.GetRequiredService<IBusDispatcher>();

        await Task.WhenAll(dispatcherA.RunOnceAsync(ct), dispatcherB.RunOnceAsync(ct));

        // A row claimed by one runner but still leased when the other polls is simply not re-claimed (lease
        // guard) — a few extra passes drain any such stragglers deterministically.
        for (var i = 0; i < 5 && counts.Count < rowCount; i++)
            await Task.WhenAll(dispatcherA.RunOnceAsync(ct), dispatcherB.RunOnceAsync(ct));

        Assert.Equal(rowCount, counts.Count);
        Assert.All(markers, marker => Assert.Equal(1, counts[marker]));
        Assert.Equal(0, await TestSupport.CountRowsAsync(fixture.ConnectionString, typeof(ConcurrencyTestEvent).AssemblyQualifiedName!));
    }
}
