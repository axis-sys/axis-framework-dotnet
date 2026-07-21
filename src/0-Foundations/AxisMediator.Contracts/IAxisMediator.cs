using Axis;
using AxisMediator.Contracts.CQRS.Handlers;

namespace AxisMediator.Contracts;

public interface IAxisMediator
{
    CancellationToken CancellationToken { get; }

    string TraceId { get; }
    string? OriginId { get; }
    string? JourneyId { get; }

    AxisEntityId? AxisEntityId { get; }

    IAxisMediatorHandler Cqrs { get; }
}
