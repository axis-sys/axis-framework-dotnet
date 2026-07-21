namespace Axis.Ports;

public interface IAxisSagaResumer
{
    Task<int> RunOnceAsync(CancellationToken cancellationToken);
}
