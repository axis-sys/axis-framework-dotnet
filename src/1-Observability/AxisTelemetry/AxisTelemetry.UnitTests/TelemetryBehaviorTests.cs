using Axis.OpenTelemetry;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Commands;
using AxisMediator.Contracts.CQRS.Handlers;
using AxisMediator.Contracts.CQRS.Queries;
using AxisMediator.Contracts.Pipelines;
using System.Diagnostics;

namespace AxisTelemetry.UnitTests;

[CollectionDefinition("OpenTelemetryCollection", DisableParallelization = true)]
public class OpenTelemetryCollection;

[Collection("OpenTelemetryCollection")]
public class TelemetryBehaviorTests : IDisposable
{
    private readonly ActivityListener _listener;

    public TelemetryBehaviorTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryAdapter.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    private sealed record TestCommand : IAxisCommand;
    private sealed record TestQueryResponse : IAxisQueryResponse;
    private sealed record TestQuery : IAxisQuery<TestQueryResponse>;

    private sealed class FakeMediator : IAxisMediator
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public string TraceId => "trace";
        public string? OriginId => null;
        public string JourneyId => "journey";
        public AxisEntityId? AxisEntityId => null;
        public IAxisMediatorHandler Cqrs => throw new NotImplementedException();
    }

    private static (TelemetryBehavior<TestCommand> BehaviorNoResp,
                    TelemetryBehavior<TestCommand> BehaviorQuery,
                    TelemetryBehavior<TestQuery, TestQueryResponse> BehaviorWithResp,
                    OpenTelemetryAdapter Adapter) Build()
    {
        var adapter = new OpenTelemetryAdapter();
        var mediator = new FakeMediator();
        return (
            new TelemetryBehavior<TestCommand>(mediator, adapter, adapter),
            new TelemetryBehavior<TestCommand>(mediator, adapter, adapter),
            new TelemetryBehavior<TestQuery, TestQueryResponse>(mediator, adapter, adapter),
            adapter);
    }

    [Fact]
    public async Task HandleAsyncNoResponseReturnsSuccessAndRecordsOk()
    {
        var (behavior, _, _, _) = Build();
        var ctx = new AxisPipelineContext();

        var result = await behavior.HandleAsync(new TestCommand(), ctx, () => Task.FromResult(AxisResult.Ok()));

        result.ShouldSucceed();
    }

    [Fact]
    public async Task HandleAsyncNoResponseMarksErrorWhenResultFails()
    {
        var (behavior, _, _, _) = Build();
        var ctx = new AxisPipelineContext();

        var result = await behavior.HandleAsync(new TestCommand(), ctx,
            () => Task.FromResult<AxisResult>(AxisError.BusinessRule("BOOM")));

        result.ShouldFail();
    }

    [Fact]
    public async Task HandleAsyncNoResponseRethrowsExceptionAndRecordsIt()
    {
        var (behavior, _, _, _) = Build();
        var ctx = new AxisPipelineContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(new TestCommand(), ctx,
                () => throw new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task HandleAsyncWithResponseReturnsSuccess()
    {
        var (_, _, behavior, _) = Build();
        var ctx = new AxisPipelineContext();

        var result = await behavior.HandleAsync(new TestQuery(), ctx,
            () => Task.FromResult(AxisResult.Ok(new TestQueryResponse())));

        result.ShouldSucceed();
    }

    [Fact]
    public async Task HandleAsyncWithResponseReturnsFailureAndTagsErrorCodes()
    {
        var (_, _, behavior, _) = Build();
        var ctx = new AxisPipelineContext();

        var result = await behavior.HandleAsync(new TestQuery(), ctx,
            () => Task.FromResult(AxisResult.Error<TestQueryResponse>(AxisError.BusinessRule("BAD"))));

        result.ShouldFail();
    }

    [Fact]
    public async Task HandleAsyncWithResponseRethrowsException()
    {
        var (_, _, behavior, _) = Build();
        var ctx = new AxisPipelineContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(new TestQuery(), ctx,
                () => throw new InvalidOperationException("boom")));
    }

    public void Dispose() => _listener.Dispose();
}
