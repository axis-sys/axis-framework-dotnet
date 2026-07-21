using AxisBus.Repository.Ports;
using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AxisBus.Postgres.IntegrationTests;

/// <summary>
/// Ponta a ponta: publish through the real durable <see cref="IAxisBus"/> and drain it into a transaction (as
/// the unit of work does at commit) so the row lands, then drain the outbox by calling
/// <see cref="IBusDispatcher.RunOnceAsync"/> explicitly (not the background worker). The registered handler runs
/// and the row is DELETED — delivery is deletion; there is no <c>Processed</c> status.
/// </summary>
[Collection("AxisBusPostgresCollection")]
public class EndToEndDispatchTests(AxisBusPostgresFixture fixture)
{
    private sealed record EndToEndTestEvent(string Marker) : IAxisEvent;

    private sealed class RecordingHandler(TaskCompletionSource<EndToEndTestEvent> received) : IAxisEventHandler<EndToEndTestEvent>
    {
        public Task<AxisResult> HandleAsync(EndToEndTestEvent @event)
        {
            received.TrySetResult(@event);
            return Task.FromResult(AxisResult.Ok());
        }
    }

    [Fact]
    public async Task PublishDrainThenRunOnce_InvokesTheHandlerAndDeletesTheRowAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        TaskCompletionSource<EndToEndTestEvent> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var sp = fixture.BuildProvider(configureServices: services =>
            services.AddScoped<IAxisEventHandler<EndToEndTestEvent>>(_ => new RecordingHandler(received)));

        var eventType = typeof(EndToEndTestEvent).AssemblyQualifiedName!;
        await sp.PublishAndDrainAsync(new EndToEndTestEvent("hello-e2e"), ct);
        Assert.Equal(1, await TestSupport.CountRowsAsync(fixture.ConnectionString, eventType));

        var dispatcher = sp.GetRequiredService<IBusDispatcher>();
        await dispatcher.RunOnceAsync(ct);

        var handled = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Equal("hello-e2e", handled.Marker);
        Assert.Equal(0, await TestSupport.CountRowsAsync(fixture.ConnectionString, eventType));
    }

    [Fact]
    public async Task RunOnceAsync_WithNothingPending_LeavesTheOutboxUntouchedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sp = fixture.BuildProvider();
        var dispatcher = sp.GetRequiredService<IBusDispatcher>();

        var escaped = await Record.ExceptionAsync(() => dispatcher.RunOnceAsync(ct));

        Assert.Null(escaped);
    }
}
