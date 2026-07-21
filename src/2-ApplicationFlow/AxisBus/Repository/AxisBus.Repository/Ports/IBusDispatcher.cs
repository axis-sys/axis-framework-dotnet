namespace AxisBus.Repository.Ports;

/// <summary>
/// Runs one dispatch pass: claim the head of each due partition, deliver it, delete on success or release on
/// failure. Returns <c>true</c> when the pass had no delivery failures, <c>false</c> otherwise — the worker
/// (<c>AxisBusDispatcherWorker</c>) uses this to drive its exponential backoff.
/// </summary>
public interface IBusDispatcher
{
    Task<bool> RunOnceAsync(CancellationToken cancellationToken);
}
