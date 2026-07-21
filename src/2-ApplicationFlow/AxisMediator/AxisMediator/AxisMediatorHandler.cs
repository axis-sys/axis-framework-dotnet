using System.Diagnostics.CodeAnalysis;
using Axis;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS;
using AxisMediator.Contracts.CQRS.Commands;
using AxisMediator.Contracts.CQRS.Handlers;
using AxisMediator.Contracts.CQRS.Queries;
using AxisMediator.Contracts.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AxisMediator;

[SuppressMessage("Performance", "CA1873:Avoid logs than can be expensive")]
internal class AxisMediatorHandler(IServiceProvider serviceProvider) : IAxisMediatorHandler
{
    public async Task<AxisResult> ExecuteAsync<TCommand>(TCommand command) where TCommand : IAxisCommand
    {
        var handler = serviceProvider.GetService<IAxisCommandHandler<TCommand>>();
        if (handler is null) return AxisError.NotFound(HandlerNotFoundMessage<TCommand>());

        return await ExecutePipelineAsync(command, () => handler.HandleAsync(command));
    }

    public async Task<AxisResult<TResponse>> ExecuteAsync<TCommand, TResponse>(TCommand command)
        where TCommand : IAxisCommand<TResponse> where TResponse : IAxisCommandResponse
    {
        var handler = serviceProvider.GetService<IAxisCommandHandler<TCommand, TResponse>>();
        if (handler is null) return AxisError.NotFound(HandlerNotFoundMessage<TCommand>());

        return await ExecutePipelineAsync(command, () => handler.HandleAsync(command));
    }

    public async Task<AxisResult<TResponse>> QueryAsync<TQuery, TResponse>(TQuery query)
        where TQuery : IAxisQuery<TResponse> where TResponse : IAxisQueryResponse
    {
        var handler = serviceProvider.GetService<IAxisQueryHandler<TQuery, TResponse>>();
        if (handler is null) return AxisError.NotFound(HandlerNotFoundMessage<TQuery>());

        return await ExecutePipelineAsync(query, () => handler.HandleAsync(query));
    }

    public async IAsyncEnumerable<TItem> StreamAsync<TQuery, TItem>(TQuery query)
        where TQuery : IAxisStreamQuery<TItem>
    {
        var handler = serviceProvider.GetService<IAxisStreamQueryHandler<TQuery, TItem>>()
            ?? throw new InvalidOperationException(HandlerNotFoundMessage<TQuery>());

        await foreach (var item in handler.HandleAsync(query).ConfigureAwait(false))
            yield return item;
    }

    private async Task<AxisResult> ExecutePipelineAsync<TRequest>(TRequest request, Func<Task<AxisResult>> handlerFunc)
        where TRequest : IAxisRequest
    {
        var context = new AxisPipelineContext();
        var behaviors = serviceProvider.GetServices<IAxisPipelineBehavior<TRequest>>().Reverse();
        var pipeline = handlerFunc;
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () => behavior.HandleAsync(request, context, next);
        }

        var result = await pipeline();
        LogResult<TRequest>(result);
        return result;
    }

    private async Task<AxisResult<TResponse>> ExecutePipelineAsync<TRequest, TResponse>(TRequest request, Func<Task<AxisResult<TResponse>>> handlerFunc)
        where TRequest : IAxisRequest
        where TResponse : IAxisResponse
    {
        var context = new AxisPipelineContext();
        var behaviors = serviceProvider.GetServices<IAxisPipelineBehavior<TRequest, TResponse>>().Reverse();
        var pipeline = handlerFunc;
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () => behavior.HandleAsync(request, context, next);
        }

        var result = await pipeline();
        LogResult<TRequest>(result);
        return result;
    }

    private void LogResult<TRequest>(AxisResult result)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<AxisMediatorHandler>>();
        var mediator = serviceProvider.GetRequiredService<IAxisMediator>();
        var requestName = typeof(TRequest).Name;
        var hasJourney = mediator.JourneyId is not null;

        if (result.IsSuccess)
        {
            if (hasJourney)
                logger.LogInformation("Handled {RequestName}, TraceId {TraceId}, JourneyId {JourneyId}", requestName, mediator.TraceId, mediator.JourneyId);
            else
                logger.LogInformation("Handled {RequestName}, TraceId {TraceId}", requestName, mediator.TraceId);
        }
        else
        {
            if (hasJourney)
                logger.LogError("Handled {RequestName} with errors, TraceId {TraceId}, JourneyId {JourneyId}, AxisErrorList: {Errors}", requestName, mediator.TraceId, mediator.JourneyId, result.Errors);
            else
                logger.LogError("Handled {RequestName} with errors, TraceId {TraceId}, AxisErrorList: {Errors}", requestName, mediator.TraceId, result.Errors);
        }
    }

    private static string HandlerNotFoundMessage<TQueryOrCommand>()
        => $"HANDLER_NOT_FOUND_{typeof(TQueryOrCommand).Name}";
}
