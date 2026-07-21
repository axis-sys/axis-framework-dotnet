using Axis.Core;

namespace Axis.Contracts.Configuration;

public static class AxisSagaDefinitions
{
    public static AxisSagaDefinition Define<TPayload>(string sagaName, Action<IAxisSagaConfigurator<TPayload>> configure) where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        var configurator = new AxisSagaConfigurator<TPayload>(sagaName);
        configure(configurator);
        return configurator.Build();
    }
}
