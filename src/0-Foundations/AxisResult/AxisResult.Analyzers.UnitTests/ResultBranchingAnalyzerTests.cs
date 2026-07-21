using Axis.Analyzers;

namespace AxisResult.Analyzers.UnitTests;

public class ResultBranchingAnalyzerTests
{
    [Fact]
    public async Task FlagsIfOnIsSuccessAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    if (r.IsSuccess) return 1;
                    return 0;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0002"));
    }

    [Fact]
    public async Task FlagsTernaryOnIsFailureAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r) => r.IsFailure ? 0 : 1;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0002"));
    }

    [Fact]
    public async Task IgnoresIsSuccessReadAsPlainValueAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                bool M(AxisResult<int> r) => r.IsSuccess;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0002"));
    }

    [Fact]
    public async Task IgnoresMatchCompositionAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r) => r.Match(v => 1, e => 0);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0002"));
    }

    // IsTransientFailure is a deliberately sanctioned exception — a different classification axis
    // (transient-vs-terminal retry policy) than the ROP rail. See rule
    // result-transient-failure-classification.
    [Fact]
    public async Task IgnoresIfOnIsTransientFailureAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    if (r.IsTransientFailure) return 1;
                    return 0;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0002"));
    }

    [Fact]
    public async Task IgnoresTernaryOnIsTransientFailureAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r) => r.IsTransientFailure ? 0 : 1;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0002"));
    }

    // The laundered form: the flag goes through a bool local before driving the branch.
    [Fact]
    public async Task FlagsBranchOnLocalLaunderedFromIsSuccessAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    var ok = r.IsSuccess;
                    if (ok) return 1;
                    return 0;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0002"));
    }

    [Fact]
    public async Task FlagsTernaryOnNegatedLaunderedLocalAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    var failed = r.IsFailure;
                    return !failed ? 1 : 0;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0002"));
    }

    // Reading the flag into a local that never branches stays allowed (assertion/return value).
    [Fact]
    public async Task IgnoresLaunderedLocalUsedAsPlainValueAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                bool M(AxisResult<int> r)
                {
                    var ok = r.IsSuccess;
                    return ok;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0002"));
    }

    // The pattern spelling of the same branch.
    [Fact]
    public async Task FlagsPropertyPatternOnIsSuccessAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    if (r is { IsSuccess: true }) return 1;
                    return 0;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0002"));
    }

    [Fact]
    public async Task IgnoresPropertyPatternOnIsTransientFailureAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    if (r is { IsTransientFailure: true }) return 1;
                    return 0;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0002"));
    }

    // The positional pattern is the sanctioned terminal exit — owned by AXIS0006, never AXIS0002.
    [Fact]
    public async Task IgnoresPositionalPatternAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    if (r is (true, var v, _)) return v;
                    return -1;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ResultBranchingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0002"));
    }
}
