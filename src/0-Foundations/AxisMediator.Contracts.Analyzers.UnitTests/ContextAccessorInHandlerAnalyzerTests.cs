using Axis.Analyzers;

namespace AxisMediator.Contracts.Analyzers.UnitTests;

public class ContextAccessorInHandlerAnalyzerTests
{
    [Fact]
    public async Task FlagsAContextAccessorInjectedIntoAHandlerAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts;
            using AxisMediator.Contracts.CQRS.Commands;

            record Ping : IAxisCommand<Pong>;
            record Pong : IAxisCommandResponse;

            class PingHandler(IAxisMediatorContextAccessor ctx) : IAxisCommandHandler<Ping, Pong>
            {
                public Task<AxisResult<Pong>> HandleAsync(Ping command) => Task.FromResult<AxisResult<Pong>>(new Pong());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ContextAccessorInHandlerAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0402"));
    }

    [Fact]
    public async Task IgnoresAHandlerThatInjectsTheMediatorAsync()
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

        var diagnostics = await AnalyzerHarness.RunAsync<ContextAccessorInHandlerAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0402"));
    }

    [Fact]
    public async Task IgnoresAContextAccessorInjectedIntoMiddlewareAsync()
    {
        const string source =
            """
            using AxisMediator.Contracts;

            class ContextMiddleware(IAxisMediatorContextAccessor ctx)
            {
                public IAxisMediatorContextAccessor Accessor => ctx;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ContextAccessorInHandlerAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0402"));
    }
}
