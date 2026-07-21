using Axis.Contracts.Configuration;
using Axis.Ports;

namespace Axis.Saga;

internal class AxisSagaDefinitionRegistry : IAxisSagaDefinitionRegistry
{
    private readonly Dictionary<string, AxisSagaDefinition> _byName;

    public AxisSagaDefinitionRegistry(IEnumerable<AxisSagaDefinition> definitions)
    {
        var list = definitions.ToList();
        All = list.AsReadOnly();
        _byName = list.ToDictionary(d => d.SagaName, StringComparer.Ordinal);
    }

    public AxisSagaDefinition? Get(string sagaName) => _byName.GetValueOrDefault(sagaName);
    public IReadOnlyCollection<AxisSagaDefinition> All { get; }
}
