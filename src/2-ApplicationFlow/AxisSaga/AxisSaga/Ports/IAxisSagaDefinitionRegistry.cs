using Axis.Contracts.Configuration;

namespace Axis.Ports;

public interface IAxisSagaDefinitionRegistry
{
    AxisSagaDefinition? Get(string sagaName);
    IReadOnlyCollection<AxisSagaDefinition> All { get; }
}
