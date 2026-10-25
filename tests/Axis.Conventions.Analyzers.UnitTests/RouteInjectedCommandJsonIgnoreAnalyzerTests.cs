namespace Axis.Conventions.Analyzers.UnitTests;

public class RouteInjectedCommandJsonIgnoreAnalyzerTests
{
    private const string Prelude =
        """
        using System.Text.Json.Serialization;
        using AxisMediator.Contracts.CQRS.Commands;

        public sealed record Resp : IAxisCommandResponse;

        """;

    [Fact]
    public async Task FlagsRouteInjectedPropertyWithoutJsonIgnoreAsync()
    {
        var source = Prelude +
                     """
                     public record Cmd : IAxisCommand<Resp>
                     {
                         public string? ProductId { get; init; }
                         public int Quantity { get; init; }
                     }

                     public static class Edge
                     {
                         public static Cmd Inject(Cmd command, string productId) => command with { ProductId = productId };
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<RouteInjectedCommandJsonIgnoreAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0605"));
    }

    [Fact]
    public async Task IgnoresRouteInjectedPropertyMarkedJsonIgnoreAsync()
    {
        var source = Prelude +
                     """
                     public record Cmd : IAxisCommand<Resp>
                     {
                         [JsonIgnore] public string? ProductId { get; init; }
                         public int Quantity { get; init; }
                     }

                     public static class Edge
                     {
                         public static Cmd Inject(Cmd command, string productId) => command with { ProductId = productId };
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<RouteInjectedCommandJsonIgnoreAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0605"));
    }

    [Fact]
    public async Task IgnoresValueThatIsNotAMethodParameterAsync()
    {
        var source = Prelude +
                     """
                     public record Cmd : IAxisCommand<Resp>
                     {
                         public string? ProductId { get; init; }
                     }

                     public static class Edge
                     {
                         public static Cmd Inject(Cmd command)
                         {
                             var fromElsewhere = "x";
                             return command with { ProductId = fromElsewhere };
                         }
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<RouteInjectedCommandJsonIgnoreAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0605"));
    }

    [Fact]
    public async Task IgnoresWithOverANonCommandRecordAsync()
    {
        var source =
            """
            public record Plain { public string? Id { get; init; } }

            public static class Edge
            {
                public static Plain Inject(Plain p, string id) => p with { Id = id };
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<RouteInjectedCommandJsonIgnoreAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0605"));
    }
}
