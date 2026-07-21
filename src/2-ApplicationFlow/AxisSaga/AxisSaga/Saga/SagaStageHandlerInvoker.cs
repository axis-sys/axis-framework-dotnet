using Axis.Contracts;
using Axis.Ports;
using Axis.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Axis.Saga;

/// <inheritdoc/>
internal class SagaStageHandlerInvoker(
    IServiceScopeFactory scopeFactory,
    IAxisLogger<SagaStageHandlerInvoker> logger
) : ISagaStageHandlerInvoker
{
    public async Task<AxisResult<object?>> InvokeAsync(Type payloadType, string sagaName, string stageName, object payload)
    {
        // Each stage runs in its OWN DI scope. Stage handlers resolve scoped infrastructure — most
        // importantly a unit of work that owns a single connection + transaction for the scope's
        // lifetime. Sharing one scope across the whole saga would let a forward stage that faults its
        // unit of work (e.g. a duplicate key aborts the transaction) poison every later stage in the
        // same execution — including the compensation chain, which could then never run and would leave
        // the saga's read-model stuck mid-flight. A saga has no single ACID transaction by design: each
        // stage is an independent unit of work, so each gets a fresh scope that is disposed (closing its
        // connection) the moment the stage returns.
        await using var scope = scopeFactory.CreateAsyncScope();
        var scopedSp = scope.ServiceProvider;

        var handlerInterface = typeof(IAxisSagaStageHandler<>).MakeGenericType(payloadType);
        var handlers = scopedSp.GetServices(handlerInterface).Where(h => h is not null).Cast<object>().ToList();

        var matched = handlers.FirstOrDefault(h => MatchesHandler(handlerInterface, h, sagaName, stageName));
        if (matched is null)
        {
            var available = string.Join(", ", handlers.Select(h => $"{h.GetType().Name} ({GetSagaName(handlerInterface, h)}/{GetStageName(handlerInterface, h)})"));
            logger.LogWarning($"No handler found for {sagaName}/{stageName} on {payloadType.Name}. Available: [{available}]");
            return AxisError.NotFound($"{AxisSagaErrors.StageHandlerNotFound}_{sagaName}_{stageName}_count_{handlers.Count}");
        }

        try
        {
            var executeMethod = handlerInterface.GetMethod(nameof(IAxisSagaStageHandler<object>.ExecuteAsync));
            if (executeMethod is null)
                return AxisError.InternalServerError("STAGE_HANDLER_EXECUTE_METHOD_NOT_FOUND");

            var taskObj = executeMethod.Invoke(matched, [payload]);
            if (taskObj is not Task task)
                return AxisError.InternalServerError("STAGE_HANDLER_RETURNED_NON_TASK");

            await task.ConfigureAwait(false);

            var resultObj = task.GetType().GetProperty("Result")?.GetValue(task);
            if (resultObj is null)
                return AxisError.InternalServerError("STAGE_HANDLER_RESULT_NULL");

            var resultType = resultObj.GetType();
            var isSuccess = (bool?)resultType.GetProperty(nameof(AxisResult.IsSuccess))?.GetValue(resultObj) ?? false;

            if (isSuccess)
            {
                var value = resultType.GetProperty(nameof(AxisResult<>.Value))?.GetValue(resultObj);
                return AxisResult.Ok(value);
            }

            var errorsObj = resultType.GetProperty(nameof(AxisResult.Errors))?.GetValue(resultObj);
            var errorList = (errorsObj as IEnumerable<AxisError>)?.ToList() ?? [];
            return AxisResult.Error<object?>(errorList);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.InnerException ?? ex.InnerException ?? ex;
            logger.LogError(inner, "Stage handler invocation threw", ("sagaName", sagaName), ("stageName", stageName));
            return AxisError.InternalServerError($"STAGE_HANDLER_THREW_{inner.GetType().Name}");
        }
    }

    private static bool MatchesHandler(Type handlerInterface, object handler, string sagaName, string stageName)
        => GetSagaName(handlerInterface, handler) == sagaName && GetStageName(handlerInterface, handler) == stageName;

    // Read SagaName/StageName off the INTERFACE type, not the concrete type: GetProperty on the concrete
    // type only sees PUBLIC members, so a handler that implements the contract explicitly
    // (string IAxisSagaStageHandler<T>.SagaName => …) would read back null and never match. The interface
    // property getter dispatches through the type's interface map, so it works for explicit and public alike.
    private static string? GetSagaName(Type handlerInterface, object handler)
        => (string?)handlerInterface.GetProperty(nameof(IAxisSagaStageHandler<>.SagaName))?.GetValue(handler);

    private static string? GetStageName(Type handlerInterface, object handler)
        => (string?)handlerInterface.GetProperty(nameof(IAxisSagaStageHandler<>.StageName))?.GetValue(handler);
}
