using Axis.Analyzers;

namespace AxisResult.Analyzers.UnitTests;

public class DeconstructOnRailAnalyzerTests
{
    [Fact]
    public async Task FlagsDeconstructionInsideResultMethodAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M(AxisResult<int> r)
                {
                    var (ok, value, errors) = r;
                    if (!ok) return r;
                    return Axis.AxisResult.Ok(value);
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0006"));
    }

    // The guard-return shape gets the contextual Then/ThenAsync suggestion in the message.
    [Fact]
    public async Task SuggestsThenForGuardReturnShapeAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M(AxisResult<int> r)
                {
                    var (ok, value, errors) = r;
                    if (!ok) return r;
                    return Axis.AxisResult.Ok(value);
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "AXIS0006");
        Assert.Contains("Then/ThenAsync", diagnostic.GetMessage());
    }

    // The hand-rolled two-arm ternary gets the Match suggestion.
    [Fact]
    public async Task SuggestsMatchForTernaryShapeAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M(AxisResult<int> r)
                {
                    var (ok, value, errors) = r;
                    return ok ? Axis.AxisResult.Ok(value) : r;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "AXIS0006");
        Assert.Contains("Match/MatchAsync", diagnostic.GetMessage());
    }

    [Fact]
    public async Task FlagsDeconstructionInsideTaskOfResultMethodAsync()
    {
        const string source =
            """
            using System.Threading.Tasks;
            using Axis;
            class C
            {
                async Task<AxisResult<int>> M(Task<AxisResult<int>> t)
                {
                    var (ok, value, errors) = await t;
                    if (!ok) return Axis.AxisResult.Error<int>(AxisError.ValidationRule("X"));
                    return Axis.AxisResult.Ok(value);
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0006"));
    }

    [Fact]
    public async Task FlagsTupleFormDeconstructionOnRailAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                Axis.AxisResult M(Axis.AxisResult r)
                {
                    (var ok, var errors) = r;
                    if (!ok) return r;
                    return Axis.AxisResult.Ok();
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0006"));
    }

    // At the terminal (a method that does NOT return AxisResult) the Deconstruct is the
    // sanctioned exit — see rule result-value-access-safety.
    [Fact]
    public async Task IgnoresDeconstructionAtTerminalAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<int> r)
                {
                    var (ok, value, errors) = r;
                    return ok ? value : -1;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0006"));
    }

    [Fact]
    public async Task FlagsPositionalPatternOnRailAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M(AxisResult<int> r)
                {
                    if (r is (true, var v, _)) return Axis.AxisResult.Ok(v + 1);
                    return r;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0006"));
    }

    [Fact]
    public async Task IgnoresPositionalPatternAtTerminalAsync()
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

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0006"));
    }

    [Fact]
    public async Task FlagsSwitchExpressionOverResultOnRailAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M(AxisResult<int> r)
                    => r switch
                    {
                        (true, var v, _) => Axis.AxisResult.Ok(v + 1),
                        _ => r,
                    };
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0006"));
    }

    [Fact]
    public async Task IgnoresPlainTupleDeconstructionOnRailAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                AxisResult<int> M((int, int) pair)
                {
                    var (a, b) = pair;
                    return Axis.AxisResult.Ok(a + b);
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DeconstructOnRailAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0006"));
    }
}
