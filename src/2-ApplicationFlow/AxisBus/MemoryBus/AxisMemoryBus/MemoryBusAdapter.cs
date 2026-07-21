using Axis;
using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AxisMemoryBus;

public class MemoryBusAdapter(IServiceProvider serviceProvider) : IAxisBus
{
    public async Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics) where TEvent : IAxisEvent
    {
        var handlers = serviceProvider.GetServices<IAxisEventHandler<TEvent>>().ToList();

        if (handlers.Count == 0)
            return AxisResult.Ok();

        var tasks = handlers.Select(handler => handler.HandleAsync(@event));

        var results = await Task.WhenAll(tasks);

        return AxisResult.Combine(results);
    }
}
