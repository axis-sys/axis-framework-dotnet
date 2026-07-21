using Axis;

namespace AxisMediator.Contracts.CQRS.Events;

public interface IAxisEventHandler<in TEvent> where TEvent : IAxisEvent
{
    Task<AxisResult> HandleAsync(TEvent @event);
}
