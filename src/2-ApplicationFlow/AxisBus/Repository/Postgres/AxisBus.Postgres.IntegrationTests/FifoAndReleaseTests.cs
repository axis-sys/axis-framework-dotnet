using AxisBus.Repository.Ports;
using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace AxisBus.Postgres.IntegrationTests;

/// <summary>
/// The two guarantees of the no-status/no-attempts model: FIFO within a partition (only the head is ever
/// claimed, so the next row waits until the head is delivered = deleted) and release-on-failure (a failed
/// delivery leaves the row in place with its lease released, re-claimable on the next pass — the worker owns
/// the backoff, not the row).
/// </summary>
[Collection("AxisBusPostgresCollection")]
public class FifoAndReleaseTests(AxisBusPostgresFixture fixture)
{
    // A fixed ordering key puts every instance in ONE logical partition, delivered head-first.
    private sealed record PartitionedEvent(string PartitionKey, int Index) : IAxisEvent
    {
        public string OrderingKey => PartitionKey;
    }

    private sealed class OrderRecordingHandler(ConcurrentQueue<int> order) : IAxisEventHandler<PartitionedEvent>
    {
        public Task<AxisResult> HandleAsync(PartitionedEvent @event)
        {
            order.Enqueue(@event.Index);
            return Task.FromResult(AxisResult.Ok());
        }
    }

    private sealed record FlakyEvent(string Marker) : IAxisEvent;

    private sealed class FailingHandler : IAxisEventHandler<FlakyEvent>
    {
        public Task<AxisResult> HandleAsync(FlakyEvent @event)
            => Task.FromResult<AxisResult>(AxisError.BusinessRule("HANDLER_FAILED"));
    }

    [Fact]
    public async Task TwoEventsInOnePartition_AreDeliveredHeadFirstAcrossPassesAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        ConcurrentQueue<int> order = new();
        var partition = $"partition-{Guid.NewGuid():N}";
        await using var sp = fixture.BuildProvider(configureServices: services =>
            services.AddScoped<IAxisEventHandler<PartitionedEvent>>(_ => new OrderRecordingHandler(order)));

        // One scope, so the two rows share the partition and are totally ordered by ENQUEUE_SEQ (0 then 1).
        await sp.PublishAllAndDrainAsync([new PartitionedEvent(partition, 0), new PartitionedEvent(partition, 1)], ct);

        var dispatcher = sp.GetRequiredService<IBusDispatcher>();
        await dispatcher.RunOnceAsync(ct);   // claims only the head (index 0), delivers it, deletes it
        await dispatcher.RunOnceAsync(ct);   // now index 1 is the partition head

        Assert.Equal([0, 1], order.ToArray());
        Assert.Equal(0, await TestSupport.CountRowsAsync(fixture.ConnectionString, typeof(PartitionedEvent).AssemblyQualifiedName!));
    }

    [Fact]
    public async Task AFailingDelivery_ReleasesTheLeaseAndKeepsTheRowForRetryAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var eventType = typeof(FlakyEvent).AssemblyQualifiedName!;
        await using var sp = fixture.BuildProvider(configureServices: services =>
            services.AddScoped<IAxisEventHandler<FlakyEvent>, FailingHandler>());

        await sp.PublishAndDrainAsync(new FlakyEvent("doomed-once"), ct);

        var clean = await sp.GetRequiredService<IBusDispatcher>().RunOnceAsync(ct);

        Assert.False(clean);
        var (exists, claimedBy) = await TestSupport.ReadClaimAsync(fixture.ConnectionString, eventType);
        Assert.True(exists);        // the row is kept in place (not deleted)
        Assert.Null(claimedBy);     // and its lease was released -> re-claimable on the next pass
    }
}
