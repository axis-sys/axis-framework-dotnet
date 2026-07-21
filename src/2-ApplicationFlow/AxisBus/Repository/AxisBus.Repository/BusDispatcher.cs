using Axis;
using AxisBus.Repository.Outbox;
using AxisBus.Repository.Ports;
using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AxisBus.Repository;

/// <summary>
/// Drains the outbox and invokes handlers — the read/dispatch side of the atomic-outbox <see cref="IAxisBus"/>
/// adapter (<see cref="RepositoryBusAdapter"/> is the write side; they never call each other). Claims the HEAD
/// of each due partition, delivers it (fan-out to every <see cref="IAxisEventHandler{TEvent}"/> in a fresh
/// scope, mirroring <c>MemoryBusAdapter</c>), then deletes on success or releases the lease on failure so the
/// partition head does not advance until it is delivered. <c>TEvent</c> is only known at runtime, so handlers
/// are resolved and invoked via reflection.
/// </summary>
/// <remarks>
/// There is no per-row retry/backoff/terminal state here: a failed delivery simply keeps its row (lease
/// released) and the pass reports failure, so the <see cref="AxisBusDispatcherWorker"/> backs the WHOLE loop
/// off exponentially and raises a critical alert — retrying indefinitely, never skipping (that is the broker's
/// job). One new <see cref="IServiceScope"/> per row isolates handler-scoped state across rows.
/// </remarks>
internal sealed class BusDispatcher(
    IBusEventDispatchStore dispatchStore,
    IServiceScopeFactory scopeFactory,
    AxisBusRepositorySettings settings,
    IAxisLogger<BusDispatcher> logger
) : IBusDispatcher
{
    private static readonly string HandleAsyncMethodName = nameof(IAxisEventHandler<IAxisEvent>.HandleAsync);

    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        // Unique per pass, not per row: every mutation in this pass (claim + delete/release) is made by the
        // same runner, mirroring AxisSagaResumerWorker's per-pass token.
        var runner = Guid.CreateVersion7().ToString();

        var heads = await dispatchStore.ClaimHeadsAsync(
            runner, (int)settings.LeaseDuration.TotalSeconds, settings.BatchSize, cancellationToken);

        var clean = true;

        // Distinct partitions are independent, but kept sequential for this first version: simplest correct
        // option (no bounded-concurrency plumbing) and a dispatcher pass is not latency-sensitive. Revisit with
        // a bounded Parallel.ForEachAsync if throughput across many partitions ever becomes the bottleneck.
        foreach (var row in heads)
        {
            if (await DispatchRowAsync(row))
            {
                await dispatchStore.DeleteDispatchedAsync(row.EventId, runner, cancellationToken);
            }
            else
            {
                await dispatchStore.ReleaseAsync(row.EventId, runner, cancellationToken);
                clean = false;
            }
        }

        return clean;
    }

    /// <summary>Delivers one row. Returns true if delivered (safe to delete), false on any failure (keep + retry).</summary>
    private async Task<bool> DispatchRowAsync(OutboxEvent row)
    {
        try
        {
            var eventType = ResolveEventType(row.EventType);
            if (eventType is null)
            {
                // A missing assembly is a deployment fault, not a poison payload — never skip it; report
                // failure so the worker alerts and retries until the type resolves.
                logger.LogError($"{nameof(BusDispatcher)} could not resolve event type", ("eventId", row.EventId), ("eventType", row.EventType));
                return false;
            }

            var handlerType = typeof(IAxisEventHandler<>).MakeGenericType(eventType);

            using var scope = scopeFactory.CreateScope();
            var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();

            // No handler registered in this process — same trivial delivery as MemoryBusAdapter when the
            // handler set is empty; the row is done and gets deleted.
            if (handlers.Count == 0)
                return true;

            var deserialized = AxisBusSerializer.Deserialize(row.PayloadJson, eventType);
            if (deserialized.IsFailure)
            {
                logger.LogError($"{nameof(BusDispatcher)} could not deserialize payload", ("eventId", row.EventId), ("eventType", row.EventType));
                return false;
            }

            var @event = deserialized.Value;
            var handleAsync = handlerType.GetMethod(HandleAsyncMethodName)
                              ?? throw new MissingMethodException(handlerType.FullName, HandleAsyncMethodName);

            // Fan-out, mirroring MemoryBusAdapter.PublishAsync: every handler invoked before any is awaited,
            // then Task.WhenAll + Combine — one slow/failing handler never blocks the others.
            var tasks = handlers
                .Select(handler => (Task<AxisResult>)handleAsync.Invoke(handler, [@event])!)
                .ToList();

            var results = await Task.WhenAll(tasks);
            return AxisResult.Combine(results).IsSuccess;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(BusDispatcher)}.{nameof(DispatchRowAsync)} failed", ("eventId", row.EventId), ("eventType", row.EventType));
            return false;
        }
    }

    // Type.GetType(string) returns null (rather than throwing) for a type/assembly it cannot find with the
    // default overload; the try/catch only guards a malformed AssemblyQualifiedName throwing during parsing.
    private Type? ResolveEventType(string assemblyQualifiedName)
    {
        try
        {
            return Type.GetType(assemblyQualifiedName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(BusDispatcher)}.{nameof(ResolveEventType)} threw", ("eventType", assemblyQualifiedName));
            return null;
        }
    }
}
