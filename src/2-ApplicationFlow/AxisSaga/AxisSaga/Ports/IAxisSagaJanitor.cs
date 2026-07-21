namespace Axis.Ports;

public interface IAxisSagaJanitor
{
    Task<int> RunOnceAsync(CancellationToken cancellationToken);
}
