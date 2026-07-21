using AxisBus.Repository.Outbox;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Events;

namespace AxisBus.Repository.UnitTests;

// The write side of the atomic outbox: RepositoryBusAdapter.PublishAsync does not touch the database and never
// invokes a handler — it serializes the event, resolves its ordering key (event key -> ambient journey -> the
// event's own id), stamps the ambient trace/journey, and ENQUEUES it on the request-scoped queue. The returned
// AxisResult is an enqueue acknowledgement; the unit of work drains the queue into its transaction at commit.
public sealed class RepositoryBusAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private sealed record TestBusEvent(string Message) : IAxisEvent;

    // A self-referencing record: System.Text.Json's default cycle detection throws JsonException when it walks
    // this, which is exactly the failure AxisBusSerializer.Serialize turns into an AxisError instead of throwing.
    private sealed record CyclicalTestEvent : IAxisEvent
    {
        public CyclicalTestEvent? Self { get; set; }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Builds the adapter over a mocked scoped queue (captures enqueued events) and an ambient mediator accessor.
    private static RepositoryBusAdapter Build(out Mock<IOutboxScopedQueue> queue, IAxisMediator? mediator = null, DateTimeOffset? now = null)
    {
        queue = new Mock<IOutboxScopedQueue>();
        queue.Setup(q => q.NextSequence()).Returns(0);
        Mock<IAxisMediatorAccessor> accessor = new();
        accessor.SetupGet(a => a.AxisMediator).Returns(mediator);
        return new RepositoryBusAdapter(queue.Object, accessor.Object, new FixedClock(now ?? Now));
    }

    [Fact]
    public async Task PublishAsync_EnqueuesAnEventWithTheExpectedShapeAsync()
    {
        Mock<IAxisMediator> mediator = new();
        mediator.SetupGet(m => m.TraceId).Returns("trace-1");
        mediator.SetupGet(m => m.JourneyId).Returns((string?)null);
        var adapter = Build(out var queue, mediator.Object);
        OutboxEvent? captured = null;
        queue.Setup(q => q.Enqueue(It.IsAny<OutboxEvent>())).Callback<OutboxEvent>(e => captured = e);

        var result = await adapter.PublishAsync(new TestBusEvent("hello"), "topic-a", "topic-b");

        result.ShouldSucceed();
        queue.Verify(q => q.Enqueue(It.IsAny<OutboxEvent>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(typeof(TestBusEvent).AssemblyQualifiedName, captured!.EventType);
        Assert.Equal(["topic-a", "topic-b"], captured.Topics);
        Assert.Equal("trace-1", captured.TraceId);
        Assert.Null(captured.JourneyId);
        Assert.Equal(Now, captured.CreatedAt);
        Assert.Contains("hello", captured.PayloadJson);
        Assert.NotEmpty(captured.EventId);
        // No explicit ordering key and no ambient journey -> the event is its own singleton partition.
        Assert.Equal(captured.EventId, captured.OrderingKey);
    }

    [Fact]
    public async Task PublishAsync_PropagatesASerializationFailureWithoutEnqueuingAsync()
    {
        var adapter = Build(out var queue);
        CyclicalTestEvent cyclical = new();
        cyclical.Self = cyclical;

        var result = await adapter.PublishAsync(cyclical);

        result.ShouldFailWithCode(AxisBusErrors.SerializationFailed);
        queue.Verify(q => q.Enqueue(It.IsAny<OutboxEvent>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WorksWithoutTopicsAsync()
    {
        var adapter = Build(out var queue);
        OutboxEvent? captured = null;
        queue.Setup(q => q.Enqueue(It.IsAny<OutboxEvent>())).Callback<OutboxEvent>(e => captured = e);

        var result = await adapter.PublishAsync(new TestBusEvent("no-topics"));

        result.ShouldSucceed();
        Assert.NotNull(captured);
        Assert.Empty(captured!.Topics);
    }

    [Fact]
    public async Task PublishAsync_FallsBackToTheAmbientJourneyIdForTheOrderingKeyAsync()
    {
        Mock<IAxisMediator> mediator = new();
        mediator.SetupGet(m => m.TraceId).Returns("trace-1");
        mediator.SetupGet(m => m.JourneyId).Returns("journey-42");
        var adapter = Build(out var queue, mediator.Object);
        OutboxEvent? captured = null;
        queue.Setup(q => q.Enqueue(It.IsAny<OutboxEvent>())).Callback<OutboxEvent>(e => captured = e);

        await adapter.PublishAsync(new TestBusEvent("flowing"));

        Assert.NotNull(captured);
        Assert.Equal("journey-42", captured!.OrderingKey);
        Assert.Equal("journey-42", captured.JourneyId);
    }
}
