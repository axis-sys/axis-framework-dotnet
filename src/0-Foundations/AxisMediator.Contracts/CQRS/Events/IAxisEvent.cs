namespace AxisMediator.Contracts.CQRS.Events;

/// <summary>
/// Marks a type as a bus event — an immutable, past-tense record carrying identifiers only.
/// </summary>
public interface IAxisEvent
{
    /// <summary>
    /// Optional logical ordering key (a partition). Events that share a key are delivered in FIFO order by
    /// the outbox; distinct keys dispatch in parallel. When null, the outbox falls back to the ambient
    /// JourneyId, then to the event's own id — so ordering is opt-in and never forced onto the domain. A
    /// per-aggregate event typically returns its aggregate id; a cross-aggregate flow leaves this null and
    /// rides the ambient JourneyId.
    /// </summary>
    string? OrderingKey => null;
}
