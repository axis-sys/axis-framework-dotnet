using Axis.Ports;
using Axis.Saga.Json;
using Axis.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Axis.Saga;

/// <inheritdoc/>
internal class SagaMediator(
    ISagaInstanceStore instances,
    IAxisSagaDefinitionRegistry registry,
    IServiceScopeFactory scopeFactory,
    IAxisLogger<SagaMediator> logger,
    // Non-null only on the keyed (per-subdomain) registration: the background run must resolve the
    // SagaEngine bound to THIS store's key, otherwise a keyed mediator would drive the default/other
    // store's engine. Null preserves the single-store-per-process behaviour verbatim.
    string? serviceKey = null
) : IAxisSagaMediator
{
    public Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload) where TPayload : class
        => StartAsync(sagaName, payload, Guid.CreateVersion7().ToString(), retainedFor: null);

    public Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId) where TPayload : class
        => StartAsync(sagaName, payload, sagaId, retainedFor: null);

    public Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, TimeSpan? retainedFor) where TPayload : class
        => StartAsync(sagaName, payload, Guid.CreateVersion7().ToString(), retainedFor);

    public async Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId, TimeSpan? retainedFor) where TPayload : class
    {
        if (string.IsNullOrWhiteSpace(sagaName))
            return AxisError.ValidationRule("SAGA_NAME_REQUIRED");

        if (string.IsNullOrWhiteSpace(sagaId))
            return AxisError.ValidationRule("SAGA_ID_REQUIRED");

        var def = registry.Get(sagaName);
        if (def is null)
            return AxisError.NotFound(AxisSagaErrors.SagaDefinitionNotFound);

        var serialized = SagaPayloadSerializer.Serialize(payload, def.PayloadType);
        if (serialized.IsFailure)
            return AxisResult.Error<string>(serialized.Errors);

        int? retainForSeconds = retainedFor.HasValue ? (int)retainedFor.Value.TotalSeconds : null;
        var insertResult = await instances.InsertAsync(sagaId, sagaName, serialized.Value, retainForSeconds);
        if (insertResult.IsFailure)
            return AxisResult.Error<string>(insertResult.Errors);

        FireAndForgetExecute(sagaId);

        return AxisResult.Ok(sagaId);
    }

    public Task<AxisResult<AxisSagaInstance>> GetByIdAsync(string sagaId) => instances.LoadAsync(sagaId);

    public async Task<AxisResult<AxisSagaInstance<TPayload>>> GetByIdAsync<TPayload>(string sagaId)
        where TPayload : class
    {
        var raw = await instances.LoadAsync(sagaId);
        if (raw.IsFailure)
            return AxisResult.Error<AxisSagaInstance<TPayload>>(raw.Errors);

        var instance = raw.Value;
        var payloadResult = SagaPayloadSerializer.Deserialize(instance.PayloadJson, typeof(TPayload));
        if (payloadResult.IsFailure)
            return AxisResult.Error<AxisSagaInstance<TPayload>>(payloadResult.Errors);

        return AxisResult.Ok(new AxisSagaInstance<TPayload>
        {
            SagaId = instance.SagaId,
            SagaName = instance.SagaName,
            Status = instance.Status,
            CurrentStage = instance.CurrentStage,
            PayloadJson = instance.PayloadJson,
            LastErrorCode = instance.LastErrorCode,
            LastErrorMessage = instance.LastErrorMessage,
            Version = instance.Version,
            CreatedAt = instance.CreatedAt,
            UpdatedAt = instance.UpdatedAt,
            Payload = (TPayload)payloadResult.Value
        });
    }

    public Task<AxisResult> ResumeAsync(string sagaId)
    {
        FireAndForgetExecute(sagaId);
        return Task.FromResult(AxisResult.Ok());
    }

    private void FireAndForgetExecute(string sagaId)
    {
        // The saga must outlive the caller. A request handler starts it, returns 202 and disconnects;
        // the ambient CancellationToken flows as an AsyncLocal, so it would be captured here and cancel
        // the saga's first database call the moment the client's request is torn down. Suppress the
        // ExecutionContext flow so the background run starts with a clean ambient context (a default,
        // non-cancelable token) that is bound to the host lifetime, not the request.
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var engine = serviceKey is null
                        ? scope.ServiceProvider.GetRequiredService<SagaEngine>()
                        : scope.ServiceProvider.GetRequiredKeyedService<SagaEngine>(serviceKey);
                    await engine.ExecuteAsync(sagaId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "SagaEngine background execution failed", ("sagaId", sagaId));
                }
                finally
                {
                    await PumpNextAsync();
                }
            });
        }
    }

    // A start is fire-and-forget: when a fan-out enqueues more sagas than the global cap allows, the
    // excess claims are denied at the lease gate and left Pending with no running task. This runs after
    // every execution: the instant a run releases its slot it fires the Pending sagas waiting on capacity,
    // so a burst drains as slots free instead of trickling in on the periodic resumer. It is meaningful
    // only under a cap (unbounded = no cap-induced backlog) and is bounded by the free capacity, so it can
    // never push concurrency past the cap. Provider-agnostic: it uses only the store ports.
    private async Task PumpNextAsync()
    {
        try
        {
            if (await instances.GetMaxConcurrentSagasAsync(CancellationToken.None) is not { } cap)
                return;

            var free = cap - await instances.CountLiveLeasesAsync(CancellationToken.None);
            if (free <= 0)
                return;

            foreach (var sagaId in await instances.ClaimStaleSagaIdsAsync(free, CancellationToken.None))
                FireAndForgetExecute(sagaId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saga pump failed");
        }
    }
}
