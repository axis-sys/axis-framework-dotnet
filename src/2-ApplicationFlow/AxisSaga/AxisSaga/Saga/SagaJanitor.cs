using Axis.Ports;

namespace Axis.Saga;

/// <inheritdoc/>
internal class SagaJanitor(ISagaInstanceStore instances) : IAxisSagaJanitor
{
    private const int BatchSize = 200;

    public Task<int> RunOnceAsync(CancellationToken cancellationToken)
        => instances.DeleteExpiredAsync(BatchSize, cancellationToken);
}
