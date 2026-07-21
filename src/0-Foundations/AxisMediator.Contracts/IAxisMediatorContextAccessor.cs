using Axis;

namespace AxisMediator.Contracts;

public interface IAxisMediatorContextAccessor
{
    string? OriginId { get; set; }
    string? JourneyId { get; set; }
    AxisEntityId? AxisEntityId { get; set; }
    CancellationToken CancellationToken { get; set; }
    bool IsAuthenticated => AxisEntityId != null;
}
