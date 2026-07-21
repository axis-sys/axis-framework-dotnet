using Axis.Analyzers;

namespace AxisResult.Analyzers.UnitTests;

public class ForgivenDeconstructedValueAnalyzerTests
{
    // A discarded success flag can never prove the value — always flagged.
    [Fact]
    public async Task FlagsForgivingWhenSuccessFlagIsDiscardedAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<string> r)
                {
                    var (_, value, errors) = r;
                    return value!.Length;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ForgivenDeconstructedValueAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0007"));
    }

    [Fact]
    public async Task FlagsForgivingWhenSuccessFlagIsNeverReadAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<string> r)
                {
                    var (ok, value, errors) = r;
                    return value!.Length;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ForgivenDeconstructedValueAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0007"));
    }

    // Reading the success flag before the `!` is the proof — the guarded terminal shape stays legal.
    [Fact]
    public async Task IgnoresForgivingAfterSuccessGuardAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<string> r)
                {
                    var (ok, value, errors) = r;
                    if (!ok) return -1;
                    return value!.Length;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ForgivenDeconstructedValueAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0007"));
    }

    [Fact]
    public async Task FlagsTupleFormWithoutProofAsync()
    {
        const string source =
            """
            using Axis;
            class C
            {
                int M(AxisResult<string> r)
                {
                    (var ok, var value, var errors) = r;
                    return value!.Length;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ForgivenDeconstructedValueAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0007"));
    }

    // `!` on locals unrelated to an AxisResult deconstruction is never this analyzer's business.
    [Fact]
    public async Task IgnoresForgivingOnUnrelatedLocalAsync()
    {
        const string source =
            """
            class C
            {
                int M()
                {
                    string? s = null;
                    return s!.Length;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ForgivenDeconstructedValueAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0007"));
    }

    [Fact]
    public async Task IgnoresForgivingOnPlainTupleDeconstructionAsync()
    {
        const string source =
            """
            class C
            {
                int M((bool, string?, int) t)
                {
                    var (ok, value, count) = t;
                    return value!.Length;
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ForgivenDeconstructedValueAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0007"));
    }
}
