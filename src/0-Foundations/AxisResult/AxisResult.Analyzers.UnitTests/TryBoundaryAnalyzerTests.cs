using Axis.Analyzers;

namespace AxisResult.Analyzers.UnitTests;

public class TryBoundaryAnalyzerTests
{
    [Fact]
    public async Task FlagsTryWithoutErrorHandlerAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M() => Axis.AxisResult.Try(() => int.Parse("1"));
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<TryBoundaryAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0004"));
    }

    [Fact]
    public async Task IgnoresTryWithATypedErrorHandlerAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M() => Axis.AxisResult.Try(() => int.Parse("1"), ex => AxisError.ValidationRule("BAD_NUMBER"));
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<TryBoundaryAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0004"));
    }

    [Fact]
    public async Task IgnoresAnUnrelatedTryMethodAsync()
    {
        const string source =
            """
            class C
            {
                int M() => Try();
                static int Try() => 0;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<TryBoundaryAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0004"));
    }
}
