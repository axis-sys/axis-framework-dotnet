namespace Axis.Conventions.Analyzers.UnitTests;

public class HandlerAccessModifierAnalyzerTests
{
    [Fact]
    public async Task FlagsPublicHandlerAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts.CQRS.Commands;
            using AxisMediator.Contracts.CQRS.Handlers;

            public sealed record Cmd : IAxisCommand<Resp>;
            public sealed record Resp : IAxisCommandResponse;

            public sealed class DoThingHandler : IAxisCommandHandler<Cmd, Resp>
            {
                public Task<AxisResult<Resp>> HandleAsync(Cmd command) => Task.FromResult((AxisResult<Resp>)new Resp());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<HandlerAccessModifierAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0600"));
    }

    [Fact]
    public async Task FlagsNonSealedInternalHandlerAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts.CQRS.Commands;
            using AxisMediator.Contracts.CQRS.Handlers;

            public sealed record Cmd : IAxisCommand<Resp>;
            public sealed record Resp : IAxisCommandResponse;

            internal class DoThingHandler : IAxisCommandHandler<Cmd, Resp>
            {
                public Task<AxisResult<Resp>> HandleAsync(Cmd command) => Task.FromResult((AxisResult<Resp>)new Resp());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<HandlerAccessModifierAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0600"));
    }

    [Fact]
    public async Task IgnoresInternalSealedHandlerAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            using AxisMediator.Contracts.CQRS.Commands;
            using AxisMediator.Contracts.CQRS.Handlers;

            public sealed record Cmd : IAxisCommand<Resp>;
            public sealed record Resp : IAxisCommandResponse;

            internal sealed class DoThingHandler : IAxisCommandHandler<Cmd, Resp>
            {
                public Task<AxisResult<Resp>> HandleAsync(Cmd command) => Task.FromResult((AxisResult<Resp>)new Resp());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<HandlerAccessModifierAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0600"));
    }

    [Fact]
    public async Task IgnoresNonHandlerClassAsync()
    {
        const string source =
            """
            public sealed class NotAHandler { }
            public class AlsoNotAHandler { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<HandlerAccessModifierAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0600"));
    }
}
