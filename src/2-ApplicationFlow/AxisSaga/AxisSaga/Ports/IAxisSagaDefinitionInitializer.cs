namespace Axis.Ports;

public interface IAxisSagaDefinitionInitializer
{
    Task<int> InitializeAsync(CancellationToken cancellationToken);
}
