using AxisMediator.Contracts;

namespace AxisMediator;

internal class AxisMediatorAccessor : IAxisMediatorAccessor
{
    private static readonly AsyncLocal<IAxisMediator?> _axisMediator = new();

    public IAxisMediator? AxisMediator
    {
        get => _axisMediator.Value;
        set => _axisMediator.Value = value;
    }
}
