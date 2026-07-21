using Axis.Analyzers;

namespace AxisResult.Analyzers.UnitTests;

public class ValueAccessSafetyAnalyzerTests
{
    [Fact]
    public async Task FlagsBareValueReadOnAxisResultAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r) => r.Value;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValueAccessSafetyAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0001"));
    }

    [Fact]
    public async Task IgnoresValueExtractedThroughMatchAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r) => r.Match(v => v, e => 0);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValueAccessSafetyAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0001"));
    }

    [Fact]
    public async Task IgnoresUnrelatedValuePropertyAsync()
    {
        const string source =
            """
            class C
            {
                int M(int? x) => x.Value;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValueAccessSafetyAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0001"));
    }
}
