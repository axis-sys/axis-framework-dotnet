using Axis.Analyzers;

namespace AxisMediator.Contracts.Analyzers.UnitTests;

public class CancellationTokenParameterAnalyzerTests
{
    [Fact]
    public async Task FlagsACancellationTokenParameterOnAHandlerMethodAsync()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts.CQRS.Commands;

            record Ping : IAxisCommand<Pong>;
            record Pong : IAxisCommandResponse;

            class PingHandler : IAxisCommandHandler<Ping, Pong>
            {
                public Task<AxisResult<Pong>> HandleAsync(Ping command) => Load(default);
                private Task<AxisResult<Pong>> Load(CancellationToken ct) => Task.FromResult<AxisResult<Pong>>(new Pong());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<CancellationTokenParameterAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0400"));
    }

    [Fact]
    public async Task IgnoresAHandlerThatReadsTheAmbientTokenAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts.CQRS.Commands;

            record Ping : IAxisCommand<Pong>;
            record Pong : IAxisCommandResponse;

            class PingHandler : IAxisCommandHandler<Ping, Pong>
            {
                public Task<AxisResult<Pong>> HandleAsync(Ping command) => Task.FromResult<AxisResult<Pong>>(new Pong());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<CancellationTokenParameterAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0400"));
    }

    [Fact]
    public async Task IgnoresACancellationTokenParameterOnANonHandlerAsync()
    {
        const string source =
            """
            using System.Threading;

            class NotAHandler
            {
                public void Work(CancellationToken ct) { }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<CancellationTokenParameterAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0400"));
    }
}
