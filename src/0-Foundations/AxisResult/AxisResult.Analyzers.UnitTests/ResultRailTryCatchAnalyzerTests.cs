using Axis.Analyzers;

namespace AxisResult.Analyzers.UnitTests;

public class ResultRailTryCatchAnalyzerTests
{
    [Fact]
    public async Task FlagsTryCatchInAxisResultMethodAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M()
                {
                    try { return 1; }
                    catch (System.Exception) { return 0; }
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultRailTryCatchAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0003"));
    }

    [Fact]
    public async Task FlagsTryCatchInAsyncTaskOfAxisResultAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            class C
            {
                async Task<AxisResult<int>> M()
                {
                    try { await Task.Yield(); return 1; }
                    catch (System.Exception) { return 0; }
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultRailTryCatchAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0003"));
    }

    [Fact]
    public async Task IgnoresTryFinallyWithoutCatchAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M()
                {
                    try { return 1; }
                    finally { }
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultRailTryCatchAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0003"));
    }

    [Fact]
    public async Task IgnoresTryCatchInNonResultMethodAsync()
    {
        const string source =
            """
            class C
            {
                int M()
                {
                    try { return 1; }
                    catch (System.Exception) { return 0; }
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultRailTryCatchAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0003"));
    }
}
