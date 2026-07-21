using Axis;
using AxisBus.Repository.Outbox;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Events;

namespace AxisBus.Repository;

/// <summary>
/// The atomic-outbox <see cref="IAxisBus"/> adapter: <see cref="PublishAsync{TEvent}"/> does not touch the
/// database and never invokes a handler — it serializes the event, resolves its ordering key, stamps the
/// ambient trace/journey, and enqueues it on the request-scoped <see cref="IOutboxScopedQueue"/>. The unit of
/// work drains that queue into its own transaction at commit (<c>RepositoryOutboxDrain</c>), so the event and
/// the business state land atomically; a separate dispatcher (<see cref="BusDispatcher"/>) delivers it after
/// the commit. The returned <see cref="AxisResult"/> is an enqueue acknowledgement, not a fan-out result.
/// </summary>
internal sealed class RepositoryBusAdapter(
    IOutboxScopedQueue queue,
    IAxisMediatorAccessor mediatorAccessor,
    TimeProvider timeProvider
) : IAxisBus
{
    public Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics) where TEvent : IAxisEvent
    {
        var payloadJson = AxisBusSerializer.Serialize(@event);
        if (payloadJson.IsFailure)
            return Task.FromResult(AxisResult.Error(payloadJson.Errors));

        var eventId = Guid.CreateVersion7().ToString();
        var mediator = mediatorAccessor.AxisMediator;
        var journeyId = mediator?.JourneyId;

        // Ordering key cascade: the event's own key (typically its aggregate id), else the ambient JourneyId
        // (a cross-aggregate flow), else the event's own id — which makes the event a singleton partition,
        // independent and time-ordered. The domain never has to reach for infrastructure to order events.
        var orderingKey = @event.OrderingKey ?? journeyId ?? eventId;

        queue.Enqueue(new OutboxEvent(
            EventId: eventId,
            OrderingKey: orderingKey,
            EnqueueSeq: queue.NextSequence(),
            EventType: typeof(TEvent).AssemblyQualifiedName!,
            PayloadJson: payloadJson.Value,
            Topics: topics,
            TraceId: mediator?.TraceId,
            JourneyId: journeyId,
            CreatedAt: timeProvider.GetUtcNow()));

        return Task.FromResult(AxisResult.Ok());
    }
}
