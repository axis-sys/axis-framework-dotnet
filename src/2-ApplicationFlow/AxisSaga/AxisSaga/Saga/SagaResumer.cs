using Axis.Ports;

namespace Axis.Saga;

/// <inheritdoc/>
internal class SagaResumer(
    ISagaInstanceStore instances,
    AxisSagaSettings settings,
    IAxisSagaMediator mediator,
    IAxisLogger<SagaResumer> logger
) : IAxisSagaResumer
{
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> staleIds;
        try
        {
            staleIds = await ClaimStaleAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resumer.ClaimStale failed");
            return 0;
        }

        foreach (var sagaId in staleIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await mediator.ResumeAsync(sagaId);
        }

        return staleIds.Count;
    }

    private async Task<IReadOnlyList<string>> ClaimStaleAsync(CancellationToken ct)
    {
        // When a global cap is set, fetch at most the number of free slots so we don't fire a batch of
        // engine runs that would immediately bounce off the cap's gate in AcquireLeaseAsync. Best-effort:
        // other instances may consume slots in between, so the engine's atomic gate stays the authority.
        var limit = settings.ResumeBatchSize;
        if (await instances.GetMaxConcurrentSagasAsync(ct) is { } max)
        {
            var free = max - await instances.CountLiveLeasesAsync(ct);
            if (free <= 0)
                return [];
            limit = Math.Min(limit, free);
        }

        return await instances.ClaimStaleSagaIdsAsync(limit, ct);
    }
}
