using AxisBus.Repository.Outbox;
using AxisBus.Repository.Ports;
using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace AxisBus.Repository.UnitTests;

// The dispatch side of the atomic outbox: BusDispatcher.RunOnceAsync claims each due partition head, delivers
// it, then DELETES the row on success or RELEASES the lease on failure (no attempts/status/retention — the
// worker owns the backoff). A pass reports `clean` = every claimed row delivered.
public sealed class BusDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private sealed record TestBusEvent(string Message) : IAxisEvent;

    private sealed class RecordingHandler(ConcurrentBag<TestBusEvent> received) : IAxisEventHandler<TestBusEvent>
    {
        public Task<AxisResult> HandleAsync(TestBusEvent @event)
        {
            received.Add(@event);
            return Task.FromResult(AxisResult.Ok());
        }
    }

    private sealed class FailingHandler : IAxisEventHandler<TestBusEvent>
    {
        public Task<AxisResult> HandleAsync(TestBusEvent @event)
            => Task.FromResult<AxisResult>(AxisError.BusinessRule("HANDLER_FAILED"));
    }

    private sealed class ThrowingHandler : IAxisEventHandler<TestBusEvent>
    {
        public Task<AxisResult> HandleAsync(TestBusEvent @event) => throw new InvalidOperationException("boom");
    }

    private static OutboxEvent MakeEvent(string eventType, string payloadJson) => new(
        EventId: Guid.CreateVersion7().ToString(),
        OrderingKey: Guid.CreateVersion7().ToString(),
        EnqueueSeq: 0,
        EventType: eventType,
        PayloadJson: payloadJson,
        Topics: [],
        TraceId: null,
        JourneyId: null,
        CreatedAt: Now.AddMinutes(-1));

    // Defaults mirror the happy path (a claimed batch, Ok deletes/releases); each test overrides only what it
    // needs — the same TestHost convention used elsewhere in the framework.
    private static Mock<IBusEventDispatchStore> BuildStore(IReadOnlyList<OutboxEvent> heads)
    {
        Mock<IBusEventDispatchStore> store = new();
        store.Setup(s => s.ClaimHeadsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(heads);
        store.Setup(s => s.DeleteDispatchedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AxisResult.Ok());
        store.Setup(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AxisResult.Ok());
        return store;
    }

    // A real (tiny) DI container is simpler than mocking IServiceScopeFactory/IServiceScope/IServiceProvider by
    // hand, and it exercises the exact GetServices(handlerType) resolution path BusDispatcher relies on.
    private static IServiceScopeFactory BuildScopeFactory(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static BusDispatcher BuildDispatcher(Mock<IBusEventDispatchStore> store, IServiceScopeFactory scopeFactory)
        => new(store.Object, scopeFactory, new AxisBusRepositorySettings
        {
            ConnectionString = "unused",
            BatchSize = 50,
            LeaseDuration = TimeSpan.FromSeconds(30),
        }, Mock.Of<IAxisLogger<BusDispatcher>>());

    [Fact]
    public async Task RunOnceAsync_WithAnEmptyBatch_DoesNothingAndReportsCleanAsync()
    {
        var store = BuildStore([]);
        var dispatcher = BuildDispatcher(store, BuildScopeFactory());

        var clean = await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.True(clean);
        store.Verify(s => s.DeleteDispatchedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_WithASucceedingHandler_DeletesTheDispatchedRowAsync()
    {
        ConcurrentBag<TestBusEvent> received = [];
        var row = MakeEvent(typeof(TestBusEvent).AssemblyQualifiedName!, AxisBusSerializer.Serialize(new TestBusEvent("hi")).Value);
        var store = BuildStore([row]);
        var scopeFactory = BuildScopeFactory(s =>
            s.AddScoped<IAxisEventHandler<TestBusEvent>>(_ => new RecordingHandler(received)));
        var dispatcher = BuildDispatcher(store, scopeFactory);

        var clean = await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.True(clean);
        store.Verify(s => s.DeleteDispatchedAsync(row.EventId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(received);
        Assert.Equal("hi", received.Single().Message);
    }

    [Fact]
    public async Task RunOnceAsync_WithNoHandlerRegistered_DeletesTheRowWithoutInvokingAnythingAsync()
    {
        var row = MakeEvent(typeof(TestBusEvent).AssemblyQualifiedName!, AxisBusSerializer.Serialize(new TestBusEvent("orphan")).Value);
        var store = BuildStore([row]);
        var dispatcher = BuildDispatcher(store, BuildScopeFactory());

        var clean = await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.True(clean);
        store.Verify(s => s.DeleteDispatchedAsync(row.EventId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_WithAFailingHandler_ReleasesTheRowAndReportsNotCleanAsync()
    {
        var row = MakeEvent(typeof(TestBusEvent).AssemblyQualifiedName!, AxisBusSerializer.Serialize(new TestBusEvent("flaky")).Value);
        var store = BuildStore([row]);
        var scopeFactory = BuildScopeFactory(s => s.AddScoped<IAxisEventHandler<TestBusEvent>, FailingHandler>());
        var dispatcher = BuildDispatcher(store, scopeFactory);

        var clean = await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.False(clean);
        store.Verify(s => s.ReleaseAsync(row.EventId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.DeleteDispatchedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheHandlerThrows_DoesNotEscapeAndReleasesTheRowAsync()
    {
        var row = MakeEvent(typeof(TestBusEvent).AssemblyQualifiedName!, AxisBusSerializer.Serialize(new TestBusEvent("boom")).Value);
        var store = BuildStore([row]);
        var scopeFactory = BuildScopeFactory(s => s.AddScoped<IAxisEventHandler<TestBusEvent>, ThrowingHandler>());
        var dispatcher = BuildDispatcher(store, scopeFactory);

        var escaped = await Record.ExceptionAsync(() => dispatcher.RunOnceAsync(CancellationToken.None));

        Assert.Null(escaped);
        store.Verify(s => s.ReleaseAsync(row.EventId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.DeleteDispatchedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_WithAnUnresolvableEventType_ReleasesTheRowInsteadOfSkippingItAsync()
    {
        var row = MakeEvent("Not.A.Real.Type, NotAnAssembly", "{}");
        var store = BuildStore([row]);
        var dispatcher = BuildDispatcher(store, BuildScopeFactory());

        var clean = await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.False(clean);
        store.Verify(s => s.ReleaseAsync(row.EventId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.DeleteDispatchedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_WithAnUndeserializablePayload_ReleasesTheRowAsync()
    {
        var row = MakeEvent(typeof(TestBusEvent).AssemblyQualifiedName!, "not-valid-json");
        var store = BuildStore([row]);
        var scopeFactory = BuildScopeFactory(s => s.AddScoped<IAxisEventHandler<TestBusEvent>>(_ => new RecordingHandler([])));
        var dispatcher = BuildDispatcher(store, scopeFactory);

        var clean = await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.False(clean);
        store.Verify(s => s.ReleaseAsync(row.EventId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.DeleteDispatchedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
