using AxisMediator.Contracts.CQRS.Events;

namespace Axis;

/// <summary>
/// Publishes events to the underlying transport. Routing details (topics, exchanges,
/// partition keys, etc.) are concerns of the adapter, not of this port.
/// </summary>
public interface IAxisBus
{
    Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics) where TEvent : IAxisEvent;
}
