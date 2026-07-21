using AxisBus.Repository.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AxisBus.Repository;

/// <summary>
/// Background poll loop that drains the outbox — the bus twin of <c>AxisSagaResumerWorker</c>. Registered only
/// when <see cref="AxisBusRepositorySettings.DispatcherEnabled"/> is set. Every
/// <see cref="AxisBusRepositorySettings.PollInterval"/> it opens a fresh <see cref="IServiceScope"/> and runs
/// <see cref="IBusDispatcher.RunOnceAsync"/> once.
/// </summary>
/// <remarks>
/// Failure handling is uniform and worker-wide, not per-event (per the outbox design): when a pass reports a
/// delivery failure — broker down, a poison event, an unresolved type — the loop backs OFF exponentially (up to
/// <see cref="AxisBusRepositorySettings.MaxPollBackoff"/>) and raises a CRITICAL log/telemetry alert so it is
/// caught immediately, but it never stops retrying and never skips a row (ordering is preserved; skipping is
/// the broker's job). A clean pass resets the backoff. The schema bootstrap is a separate hosted service
/// (<see cref="AxisBusStorageInitializerWorker"/>), so this worker does not touch it.
/// </remarks>
internal sealed class AxisBusDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    AxisBusRepositorySettings settings,
    ILogger<AxisBusDispatcherWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            bool clean;
            try
            {
                using var scope = scopeFactory.CreateScope();
                clean = await scope.ServiceProvider.GetRequiredService<IBusDispatcher>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                clean = false;
                logger.LogError(ex, "Outbox dispatch pass threw.");
            }

            TimeSpan delay;
            if (clean)
            {
                consecutiveFailures = 0;
                delay = settings.PollInterval;
            }
            else
            {
                consecutiveFailures++;
                // Exponential backoff of the WHOLE worker (uniform, not per-event), capped at MaxPollBackoff.
                var seconds = Math.Min(
                    settings.PollInterval.TotalSeconds * Math.Pow(2, consecutiveFailures),
                    settings.MaxPollBackoff.TotalSeconds);
                delay = TimeSpan.FromSeconds(seconds);
                logger.LogCritical(
                    "Outbox dispatch failing ({ConsecutiveFailures} consecutive passes); backing off to {DelaySeconds}s and retrying. Investigate immediately — delivery is halted until this clears.",
                    consecutiveFailures, delay.TotalSeconds);
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
