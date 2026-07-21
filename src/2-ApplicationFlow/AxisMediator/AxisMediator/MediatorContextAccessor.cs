using Axis;
using AxisMediator.Contracts;

namespace AxisMediator;

internal class AxisMediatorContextAccessor : IAxisMediatorContextAccessor
{
    private static readonly AsyncLocal<string?> _originId = new();
    private static readonly AsyncLocal<string?> _journeyId = new();
    private static readonly AsyncLocal<CancellationToken> _cancellationToken = new();
    private static readonly AsyncLocal<AxisEntityId?> _axisEntityId = new();

    public string? OriginId
    {
        get => _originId.Value;
        set => _originId.Value = value;
    }

    public string? JourneyId
    {
        get => _journeyId.Value;
        set => _journeyId.Value = value;
    }

    public CancellationToken CancellationToken
    {
        get => _cancellationToken.Value;
        set => _cancellationToken.Value = value;
    }

    public AxisEntityId? AxisEntityId
    {
        get => _axisEntityId.Value;
        set => _axisEntityId.Value = value;
    }
}
