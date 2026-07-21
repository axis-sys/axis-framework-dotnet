using System.Diagnostics;
using Axis;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Handlers;

namespace AxisMediator;

internal class AxisMediator : IAxisMediator, IDisposable
{
    private readonly IAxisMediatorAccessor _accessor;
    private readonly IAxisMediatorContextAccessor _contextAccessor;
    public IAxisMediatorHandler Cqrs { get; }
    public string? OriginId => _contextAccessor.OriginId;
    public string? JourneyId => _contextAccessor.JourneyId;
    public AxisEntityId? AxisEntityId => _contextAccessor.AxisEntityId;
    public CancellationToken CancellationToken => _contextAccessor.CancellationToken;

    public AxisMediator(
        IAxisMediatorHandler cqrs,
        IAxisMediatorAccessor accessor,
        IAxisMediatorContextAccessor contextAccessor
    )
    {
        Cqrs = cqrs;
        _accessor = accessor;
        _contextAccessor = contextAccessor;
        _accessor.AxisMediator = this;
    }

    public string TraceId { get; } = ResolveTraceId();
    private static string ResolveTraceId()
    {
        return Activity.Current is not { } activity
            ? Guid.NewGuid().ToString()
            : activity.TraceId.ToString();
    }

    public void Dispose() => _accessor.AxisMediator = null;
}
