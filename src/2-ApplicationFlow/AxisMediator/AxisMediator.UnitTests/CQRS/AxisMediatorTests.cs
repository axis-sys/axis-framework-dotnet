using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Commands;
using AxisMediator.Contracts.CQRS.Handlers;
using AxisMediator.Contracts.CQRS.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace AxisMediator.UnitTests.CQRS;

public class AxisMediatorTests : BaseUnitTest
{
    public sealed record TestStreamItem(int Value);
    public sealed record TestStreamQuery(int Count) : IAxisStreamQuery<TestStreamItem>;
    public sealed class TestStreamHandler : IAxisStreamQueryHandler<TestStreamQuery, TestStreamItem>
    {
        public async IAsyncEnumerable<TestStreamItem> HandleAsync(TestStreamQuery query)
        {
            for (var i = 0; i < query.Count; i++)
            {
                yield return new TestStreamItem(i);
                await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task StreamAsyncYieldsAllItems()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        var items = new List<TestStreamItem>();
        await foreach (var item in mediator.Cqrs.StreamAsync<TestStreamQuery, TestStreamItem>(new TestStreamQuery(3)))
            items.Add(item);

        Assert.Equal(3, items.Count);
    }

    public sealed record MissingStreamQuery : IAxisStreamQuery<TestStreamItem>;

    [Fact]
    public async Task StreamAsyncThrowsWhenHandlerMissing()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in mediator.Cqrs.StreamAsync<MissingStreamQuery, TestStreamItem>(new MissingStreamQuery()))
            {
            }
        });
    }

    public sealed record SlowCommand : IAxisCommand<SlowResponse>
    {
        public int DelayMs { get; init; }
    }

    public sealed record SlowResponse : IAxisCommandResponse
    {
        public required bool Done { get; init; }
    }

    public sealed class SlowHandler : IAxisCommandHandler<SlowCommand, SlowResponse>
    {
        public async Task<AxisResult<SlowResponse>> HandleAsync(SlowCommand command)
        {
            await Task.Delay(command.DelayMs);
            return new SlowResponse { Done = true };
        }
    }

    [Fact]
    public async Task PerformanceBehaviorLogsSlowRequestOverThreshold()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        var result = await mediator.Cqrs.ExecuteAsync<SlowCommand, SlowResponse>(new SlowCommand { DelayMs = 550 });

        result.ShouldSucceed();
    }
    private sealed record MissingCommand : IAxisCommand;
    private sealed record MissingCommandWithResp : IAxisCommand<MissingResponse>;
    private sealed record MissingResponse : IAxisCommandResponse;
    private sealed record MissingQueryResponse : IAxisQueryResponse;
    private sealed record MissingQuery : IAxisQuery<MissingQueryResponse>;

    [Fact]
    public async Task ExecuteAsyncReturnsHandlerNotFoundWhenHandlerMissing()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        var result = await mediator.Cqrs.ExecuteAsync(new MissingCommand());

        result.ShouldFail();
        Assert.Contains(result.Errors, e => e.Code.StartsWith("HANDLER_NOT_FOUND_"));
    }

    [Fact]
    public async Task ExecuteAsyncWithResponseReturnsHandlerNotFoundWhenHandlerMissing()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        var result = await mediator.Cqrs.ExecuteAsync<MissingCommandWithResp, MissingResponse>(new MissingCommandWithResp());

        result.ShouldFail();
        Assert.Contains(result.Errors, e => e.Code.StartsWith("HANDLER_NOT_FOUND_"));
    }

    [Fact]
    public async Task QueryAsyncReturnsHandlerNotFoundWhenHandlerMissing()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        var result = await mediator.Cqrs.QueryAsync<MissingQuery, MissingQueryResponse>(new MissingQuery());

        result.ShouldFail();
        Assert.Contains(result.Errors, e => e.Code.StartsWith("HANDLER_NOT_FOUND_"));
    }

    [Fact]
    public void TraceIdIsAvailable()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        Assert.False(string.IsNullOrWhiteSpace(mediator.TraceId));
    }

    [Fact]
    public void AxisIdentityIsSetFromContext()
    {
        using var scope = DefaultServiceProvider().CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();

        Assert.NotNull(mediator.AxisEntityId);
    }

    [Fact]
    public void DisposeClearsAccessorMediator()
    {
        var provider = DefaultServiceProvider();
        var accessor = provider.GetRequiredService<IAxisMediatorAccessor>();
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IAxisMediator>();
            Assert.Same(mediator, accessor.AxisMediator);
        }
        Assert.Null(accessor.AxisMediator);
    }
}

public class AxisMediatorContextAccessorTests
{
    [Fact]
    public void OriginIdAndJourneyIdAndAxisIdentityAndCancellationTokenRoundtrip()
    {
        var services = new ServiceCollection().AddAxisMediator().BuildServiceProvider();
        var accessor = services.GetRequiredService<IAxisMediatorContextAccessor>();

        var axisIdentity = AxisEntityId.New;
        accessor.OriginId = "origin";
        accessor.JourneyId = "journey";
        accessor.AxisEntityId = axisIdentity;
        using var cts = new CancellationTokenSource();
        accessor.CancellationToken = cts.Token;

        Assert.Equal("origin", accessor.OriginId);
        Assert.Equal("journey", accessor.JourneyId);
        Assert.Equal(axisIdentity, accessor.AxisEntityId);
        Assert.Equal(cts.Token, accessor.CancellationToken);
    }
}

public class AxisMediatorAccessorTests
{
    private sealed class StubMediator : IAxisMediator
    {
        public CancellationToken CancellationToken => default;
        public string TraceId => "t";
        public string? OriginId => null;
        public string? JourneyId => null;
        public AxisEntityId? AxisEntityId => null;
        public IAxisMediatorHandler Cqrs => null!;
    }

    [Fact]
    public void AccessorAsyncLocalRoundtripsMediatorInstance()
    {
        var services = new ServiceCollection().AddAxisMediator().BuildServiceProvider();
        var accessor = services.GetRequiredService<IAxisMediatorAccessor>();

        var stub = new StubMediator();
        accessor.AxisMediator = stub;

        Assert.Same(stub, accessor.AxisMediator);

        accessor.AxisMediator = null;
        Assert.Null(accessor.AxisMediator);
    }
}
