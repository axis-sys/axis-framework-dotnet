namespace AxisBus.Repository.Outbox;

/// <summary>
/// One queued outbox event, captured at publish time and drained into the business transaction at commit.
/// <see cref="OrderingKey"/> is the resolved logical partition; <see cref="EnqueueSeq"/> is the local,
/// per-request enqueue order (the tie-breaker for events co-created in the same instant — invisible to the
/// domain). This same record is also the row shape read back on the dispatch side.
/// </summary>
public sealed record OutboxEvent(
    string EventId,
    string OrderingKey,
    int EnqueueSeq,
    string EventType,
    string PayloadJson,
    string[] Topics,
    string? TraceId,
    string? JourneyId,
    DateTimeOffset CreatedAt);

/// <summary>
/// The request-scoped queue that collects events published during a unit of work. Publishing enqueues here
/// (never touches the database); <c>RepositoryOutboxDrain</c> flushes the queue into the unit of work's own
/// connection/transaction just before commit, so the events land atomically with the business state.
/// </summary>
/// <remarks>
/// Scoped (one per request/unit of work). Not thread-safe by design: a single unit of work is driven from one
/// logical flow — the ambient-context model (AsyncLocal) the mediator already assumes.
/// </remarks>
internal interface IOutboxScopedQueue
{
    /// <summary>Returns the next per-request enqueue sequence (0, 1, 2, …), assigned in publish order.</summary>
    int NextSequence();

    /// <summary>Adds an event to be drained at commit.</summary>
    void Enqueue(OutboxEvent pending);

    /// <summary>Returns and clears the queued events (in enqueue order).</summary>
    IReadOnlyList<OutboxEvent> DrainAll();
}

/// <inheritdoc cref="IOutboxScopedQueue"/>
internal sealed class OutboxScopedQueue : IOutboxScopedQueue
{
    private readonly List<OutboxEvent> _pending = [];
    private int _sequence;

    public int NextSequence() => _sequence++;

    public void Enqueue(OutboxEvent pending) => _pending.Add(pending);

    public IReadOnlyList<OutboxEvent> DrainAll()
    {
        if (_pending.Count == 0)
            return [];

        OutboxEvent[] copy = [.. _pending];
        _pending.Clear();
        return copy;
    }
}
