using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.Saga.Json;
using Axis.SharedKernel;

namespace Axis.Saga;

/// <summary>
/// Orchestrates a single saga instance through its forward chain or its compensation chain. Fully
/// dialect-agnostic: it talks only to the <see cref="ISagaInstanceStore"/> / <see cref="ISagaStageLogStore"/>
/// ports and (de)serializes payloads itself, so the same engine drives Postgres, MySQL or any future
/// storage adapter.
///
/// Single-execution is enforced by a connection-less LEASE rather than a held advisory lock: one run
/// claims the instance (<see cref="ISagaInstanceStore.AcquireLeaseAsync"/>), a background heartbeat
/// keeps the lease fresh while stages run, and every persist is guarded by lease ownership so a run
/// that lost its lease cannot mutate the saga.
/// </summary>
internal class SagaEngine(
    IAxisSagaDefinitionRegistry registry,
    ISagaInstanceStore instances,
    ISagaStageLogStore logs,
    ISagaStageHandlerInvoker handlers,
    AxisSagaSettings settings,
    IAxisLogger<SagaEngine> logger
)
{
    public async Task<AxisResult> ExecuteAsync(string sagaId)
    {
        var runner = Guid.CreateVersion7().ToString();
        var leaseSeconds = Math.Max(1, (int)settings.ResumeAfter.TotalSeconds);

        var instance = await instances.AcquireLeaseAsync(sagaId, runner, leaseSeconds);
        if (instance is null)
            return AxisResult.Ok();

        using var leaseCts = new CancellationTokenSource();
        var heartbeat = HeartbeatAsync(sagaId, runner, leaseSeconds, leaseCts);
        try
        {
            var def = registry.Get(instance.SagaName);
            if (def is null)
                return await instances.FailAsync(sagaId, instance.Version, runner, AxisSagaErrors.SagaDefinitionNotFound);

            var payloadResult = SagaPayloadSerializer.Deserialize(instance.PayloadJson, def.PayloadType);
            if (payloadResult.IsFailure)
                return await instances.FailAsync(sagaId, instance.Version, runner, FirstCode(payloadResult.Errors));

            var ctx = new SagaExecution(sagaId, runner, def, payloadResult.Value, instance.Version, leaseCts.Token);
            var startStage = instance.CurrentStage ?? def.FirstForwardStage.StageName;

            return instance.Status == AxisSagaStatus.Compensating
                ? await ContinueCompensationAsync(ctx, startStage)
                : await ExecuteForwardChainAsync(ctx, startStage);
        }
        finally
        {
            await leaseCts.CancelAsync();
            await heartbeat;
        }
    }

    // ─────────────────────────── Lease heartbeat ───────────────────────────

    private async Task HeartbeatAsync(string sagaId, string runner, int leaseSeconds, CancellationTokenSource leaseCts)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1.0, leaseSeconds / 4.0));
        try
        {
            while (!leaseCts.IsCancellationRequested)
            {
                await Task.Delay(interval, leaseCts.Token);
                if (leaseCts.IsCancellationRequested)
                    break;
                if (!await instances.ExtendLeaseAsync(sagaId, runner, leaseSeconds))
                {
                    await leaseCts.CancelAsync();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: the engine finished and cancelled the heartbeat.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saga heartbeat failed", ("sagaId", sagaId));
        }
    }

    // ─────────────────────────── Forward chain ───────────────────────────

    private async Task<AxisResult> ExecuteForwardChainAsync(SagaExecution ctx, string startStage)
    {
        var currentStage = startStage;

        while (currentStage is not null)
        {
            if (ctx.LeaseToken.IsCancellationRequested)
                return AxisResult.Ok();

            var stageDef = ctx.RequireStage(currentStage);

            if (await TrySkipCompletedStageAsync(ctx, currentStage))
            {
                currentStage = stageDef.NextStageOnSuccess;
                continue;
            }

            var transition = await TransitionToAsync(ctx, AxisSagaStatus.Running, currentStage);
            if (transition.IsFailure)
                return transition;

            var outcome = await ExecuteStageAsync(ctx, stageDef, currentStage, keepCurrentStage: false);
            if (outcome.PersistenceFailure is { } persistFailure)
                return persistFailure;

            if (outcome.IsSuccess)
            {
                currentStage = stageDef.NextStageOnSuccess;
                continue;
            }

            if (stageDef.RouteToOnError.Count == 0)
                return await instances.FailAsync(ctx.SagaId, ctx.Version, ctx.Runner, outcome.FirstErrorCode!, outcome.AllErrorCodes);

            var beginCompensation = await TransitionToAsync(
                ctx, AxisSagaStatus.Compensating, currentStage, outcome.FirstErrorCode, outcome.AllErrorCodes);
            if (beginCompensation.IsFailure)
                return beginCompensation;

            return await ExecuteCompensationChainAsync(ctx, stageDef.RouteToOnError);
        }

        return await instances.CompleteAsync(ctx.SagaId, ctx.Version, ctx.Runner);
    }

    // ─────────────────────────── Compensation chain ───────────────────────────

    private async Task<AxisResult> ContinueCompensationAsync(SagaExecution ctx, string currentStage)
    {
        var fromStage = ctx.Def.GetStage(currentStage);
        if (fromStage is null)
            return await instances.FailAsync(ctx.SagaId, ctx.Version, ctx.Runner, AxisSagaErrors.StageNotFound);

        return await ExecuteCompensationChainAsync(ctx, fromStage.RouteToOnError);
    }

    private async Task<AxisResult> ExecuteCompensationChainAsync(SagaExecution ctx, IReadOnlyList<string> chain)
    {
        foreach (var compStageName in chain)
        {
            if (ctx.LeaseToken.IsCancellationRequested)
                return AxisResult.Ok();

            var stageDef = ctx.Def.GetStage(compStageName);
            if (stageDef is null)
                return await instances.FailAsync(ctx.SagaId, ctx.Version, ctx.Runner, AxisSagaErrors.StageNotFound);

            if (await TrySkipCompletedStageAsync(ctx, compStageName))
                continue;

            var transition = await TransitionToAsync(ctx, AxisSagaStatus.Compensating, compStageName);
            if (transition.IsFailure)
                return transition;

            var outcome = await ExecuteStageAsync(ctx, stageDef, compStageName, keepCurrentStage: true);
            if (outcome.PersistenceFailure is { } persistFailure)
                return persistFailure;

            if (!outcome.IsSuccess)
                return await instances.FailAsync(ctx.SagaId, ctx.Version, ctx.Runner, outcome.FirstErrorCode!, outcome.AllErrorCodes);
        }

        return await instances.CompensateAsync(ctx.SagaId, ctx.Version, ctx.Runner);
    }

    // ─────────────────────────── Per-stage execution ───────────────────────────

    private async Task<StageOutcome> ExecuteStageAsync(
        SagaExecution ctx, AxisSagaStageDefinition stageDef,
        string stageName, bool keepCurrentStage)
    {
        var logId = await logs.WriteStartedAsync(ctx.SagaId, stageName);
        var execResult = await InvokeWithTransientRetryAsync(ctx, stageDef, stageName);

        if (execResult.IsSuccess)
        {
            ctx.Payload = execResult.Value!;

            var serialized = SagaPayloadSerializer.Serialize(ctx.Payload, ctx.Def.PayloadType);
            if (serialized.IsFailure)
                return StageOutcome.PersistenceFailed(AxisResult.Error(serialized.Errors));

            var persist = await instances.PersistStageSuccessAsync(
                ctx.SagaId, ctx.Version, ctx.Runner, stageName, serialized.Value, keepCurrentStage);
            if (persist.IsFailure)
                return StageOutcome.PersistenceFailed(persist);

            ctx.Version++;
            await logs.MarkCompletedAsync(logId);
            return StageOutcome.Success();
        }

        var firstCode = FirstCode(execResult.Errors);
        var allCodes = JoinCodes(execResult.Errors);
        await logs.MarkFailedAsync(logId, firstCode, allCodes);
        return StageOutcome.Failed(firstCode, allCodes);
    }

    // Re-invokes the handler in place while it fails transiently (deadlock, serialization, connection blip —
    // any AxisError.IsTransient), up to the stage's attempt cap. The retry stays inside the current lease
    // (the heartbeat keeps it alive) and is invisible to the stage log: WriteStarted/MarkCompleted/MarkFailed
    // still fire once, around the final outcome. Handlers are idempotent by contract, so re-running a stage
    // whose transaction was rolled back by the transient is safe.
    private async Task<AxisResult<object?>> InvokeWithTransientRetryAsync(
        SagaExecution ctx, AxisSagaStageDefinition stageDef, string stageName)
    {
        var maxAttempts = stageDef.TransientRetryMaxAttempts ?? settings.TransientRetryMaxAttempts;
        var baseDelay = stageDef.TransientRetryBaseDelay ?? settings.TransientRetryBaseDelay;

        for (var attempt = 1; ; attempt++)
        {
            var execResult = await handlers.InvokeAsync(ctx.Def.PayloadType, ctx.Def.SagaName, stageName, ctx.Payload);

            if (execResult.IsSuccess || !execResult.IsTransientFailure
                || attempt >= maxAttempts || ctx.LeaseToken.IsCancellationRequested)
                return execResult;

            logger.LogWarning(
                "Saga stage transient failure; retrying",
                ("sagaName", ctx.Def.SagaName), ("stageName", stageName),
                ("attempt", attempt), ("maxAttempts", maxAttempts), ("errorCodes", JoinCodes(execResult.Errors)));

            try
            {
                await Task.Delay(BackoffDelay(baseDelay, attempt), ctx.LeaseToken);
            }
            catch (OperationCanceledException)
            {
                return execResult;
            }
        }
    }

    // Backoff scales with the attempt number; the jitter desynchronizes racing runners so a retried
    // deadlock does not immediately re-collide with the same contender.
    private static TimeSpan BackoffDelay(TimeSpan baseDelay, int attempt)
        => baseDelay * attempt + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 25));

    private async Task<bool> TrySkipCompletedStageAsync(SagaExecution ctx, string stageName)
    {
        if (!await logs.IsCompletedAsync(ctx.SagaId, stageName))
            return false;

        // Stage already completed in a prior run — refresh the in-memory payload from the persisted row
        // so the next stage sees the same view it would have had then.
        var json = await instances.ReloadPayloadJsonAsync(ctx.SagaId);
        if (json is not null)
        {
            var reloaded = SagaPayloadSerializer.Deserialize(json, ctx.Def.PayloadType);
            if (reloaded.IsSuccess)
                ctx.Payload = reloaded.Value;
        }
        return true;
    }

    private async Task<AxisResult> TransitionToAsync(
        SagaExecution ctx, AxisSagaStatus newStatus, string? currentStage,
        string? errorCode = null, string? errorMessage = null)
    {
        var moveResult = await instances.MoveToStatusAsync(
            ctx.SagaId, ctx.Version, ctx.Runner, newStatus, currentStage, errorCode, errorMessage);
        if (moveResult.IsSuccess)
            ctx.Version++;
        return moveResult;
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static string FirstCode(IEnumerable<AxisError> errors)
        => errors.Select(e => e.Code).FirstOrDefault() ?? AxisSagaErrors.PersistenceFailed;

    private static string JoinCodes(IEnumerable<AxisError> errors)
        => string.Join("; ", errors.Select(e => e.Code));

    private sealed class SagaExecution(
        string sagaId, string runner, AxisSagaDefinition def, object payload, int version, CancellationToken leaseToken)
    {
        public string SagaId { get; } = sagaId;
        public string Runner { get; } = runner;
        public AxisSagaDefinition Def { get; } = def;
        public object Payload { get; set; } = payload;
        public int Version { get; set; } = version;
        public CancellationToken LeaseToken { get; } = leaseToken;

        public AxisSagaStageDefinition RequireStage(string stageName)
            => Def.GetStage(stageName)
               ?? throw new InvalidOperationException(
                   $"Stage '{stageName}' missing from definition '{Def.SagaName}'.");
    }

    private readonly record struct StageOutcome(
        bool IsSuccess,
        string? FirstErrorCode,
        string? AllErrorCodes,
        AxisResult? PersistenceFailure)
    {
        public static StageOutcome Success() => new(true, null, null, null);
        public static StageOutcome Failed(string firstCode, string allCodes) => new(false, firstCode, allCodes, null);
        public static StageOutcome PersistenceFailed(AxisResult failure) => new(false, null, null, failure);
    }
}
