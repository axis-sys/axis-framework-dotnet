using System.Diagnostics;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS;
using AxisMediator.Contracts.CQRS.Queries;
using AxisMediator.Contracts.Pipelines;

namespace Axis;

public class TelemetryBehavior<TRequest>(
    IAxisMediator mediator,
    IAxisTelemetry telemetry,
    IAxisMetrics metrics
) : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var requestName = typeof(TRequest).Name;
        using var span = telemetry.StartSpan($"AxisMediator.{requestName}");
        context.Set(AxisPipelineContextKeys.Span, span);

        span.SetTag(TelemetryTagNames.TraceId, mediator.TraceId)
            .SetTag(TelemetryTagNames.JourneyId, mediator.JourneyId)
            .SetTag(TelemetryTagNames.RequestType, "command")
            .SetTag(TelemetryTagNames.AxisEntityId, mediator.AxisEntityId)
            .SetTag(TelemetryTagNames.RequestName, requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next();
            sw.Stop();

            RecordResult(span, requestName, result.IsSuccess, sw.Elapsed.TotalMilliseconds);

            if (result.IsFailure)
                span.SetTag(TelemetryTagNames.ErrorCodes, string.Join(", ", result.Errors.Select(e => e.Code)));

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            span.RecordException(ex);
            metrics.IncrementCounter("axis.handler.exceptions",
                tags: [new(TelemetryTagNames.RequestName, requestName), new(TelemetryTagNames.ExceptionType, ex.GetType().Name)]);
            throw;
        }
    }

    private void RecordResult(IAxisSpan span, string requestName, bool success, double durationMs)
    {
        span.SetTag(TelemetryTagNames.ResultSuccess, success)
            .SetStatus(success ? AxisSpanStatus.Ok : AxisSpanStatus.Error);

        metrics.RecordHistogram("axis.handler.duration_ms", durationMs,
            new(TelemetryTagNames.RequestName, requestName),
            new(TelemetryTagNames.ResultSuccess, success));

        metrics.IncrementCounter("axis.handler.invocations",
            tags: [new(TelemetryTagNames.RequestName, requestName), new(TelemetryTagNames.ResultSuccess, success)]);
    }
}

public class TelemetryBehavior<TRequest, TResponse>(
    IAxisMediator mediator,
    IAxisTelemetry telemetry,
    IAxisMetrics metrics
) : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    public async Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        var requestName = typeof(TRequest).Name;
        using var span = telemetry.StartSpan($"AxisMediator.{requestName}");
        context.Set(AxisPipelineContextKeys.Span, span);

        span.SetTag(TelemetryTagNames.TraceId, mediator.TraceId)
            .SetTag(TelemetryTagNames.JourneyId, mediator.JourneyId)
            .SetTag(TelemetryTagNames.RequestType, request is IAxisQuery ? "query" : "command")
            .SetTag(TelemetryTagNames.AxisEntityId, mediator.AxisEntityId)
            .SetTag(TelemetryTagNames.RequestName, requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next();
            sw.Stop();

            RecordResult(span, requestName, result.IsSuccess, sw.Elapsed.TotalMilliseconds);

            if (result.IsFailure)
                span.SetTag(TelemetryTagNames.ErrorCodes, string.Join(", ", result.Errors.Select(e => e.Code)));

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            span.RecordException(ex);
            metrics.IncrementCounter("axis.handler.exceptions",
                tags: [new(TelemetryTagNames.RequestName, requestName), new(TelemetryTagNames.ExceptionType, ex.GetType().Name)]);
            throw;
        }
    }

    private void RecordResult(IAxisSpan span, string requestName, bool success, double durationMs)
    {
        span.SetTag(TelemetryTagNames.ResultSuccess, success)
            .SetStatus(success ? AxisSpanStatus.Ok : AxisSpanStatus.Error);

        metrics.RecordHistogram("axis.handler.duration_ms", durationMs,
            new(TelemetryTagNames.RequestName, requestName),
            new(TelemetryTagNames.ResultSuccess, success));

        metrics.IncrementCounter("axis.handler.invocations",
            tags: [new(TelemetryTagNames.RequestName, requestName), new(TelemetryTagNames.ResultSuccess, success)]);
    }
}
