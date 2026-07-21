using Axis.Analyzers;

namespace AxisMediator.Contracts.Analyzers.UnitTests;

public class MediatorDispatchInHandlerAnalyzerTests
{
    [Fact]
    public async Task FlagsMediatorCqrsAccessInsideAHandlerAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts;
            using AxisMediator.Contracts.CQRS.Commands;

            record Ping : IAxisCommand<Pong>;
            record Pong : IAxisCommandResponse;

            class PingHandler(IAxisMediator mediator) : IAxisCommandHandler<Ping, Pong>
            {
                public Task<AxisResult<Pong>> HandleAsync(Ping command)
                {
                    var cqrs = mediator.Cqrs;
                    return Task.FromResult<AxisResult<Pong>>(new Pong());
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<MediatorDispatchInHandlerAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0401"));
    }

    [Fact]
    public async Task IgnoresMediatorCqrsAccessInsideAFacadeAsync()
    {
        const string source =
            """
            using AxisMediator.Contracts;

            class PingFacade(IAxisMediator mediator)
            {
                public object Dispatch() => mediator.Cqrs;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<MediatorDispatchInHandlerAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0401"));
    }

    [Fact]
    public async Task IgnoresAHandlerThatDoesNotDispatchAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts;
            using AxisMediator.Contracts.CQRS.Commands;

            record Ping : IAxisCommand<Pong>;
            record Pong : IAxisCommandResponse;

            class PingHandler(IAxisMediator mediator) : IAxisCommandHandler<Ping, Pong>
            {
                public Task<AxisResult<Pong>> HandleAsync(Ping command) => Task.FromResult<AxisResult<Pong>>(new Pong());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<MediatorDispatchInHandlerAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0401"));
    }
}
