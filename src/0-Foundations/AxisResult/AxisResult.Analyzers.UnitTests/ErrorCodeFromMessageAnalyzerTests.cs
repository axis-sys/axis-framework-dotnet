using Axis.Analyzers;

namespace AxisResult.Analyzers.UnitTests;

public class ErrorCodeFromMessageAnalyzerTests
{
    [Fact]
    public async Task FlagsAnAxisErrorCodeBuiltFromAnExceptionMessageAsync()
    {
        const string source =
            """
            using Axis;
            using System;
            class C
            {
                Axis.AxisResult M(Exception ex) => AxisError.InternalServerError(ex.Message);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorCodeFromMessageAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0005"));
    }

    [Fact]
    public async Task IgnoresAStableConstantCodeAsync()
    {
        const string source =
            """
            using Axis;
            using System;
            class C
            {
                Axis.AxisResult M(Exception ex) => AxisError.InternalServerError("POSTGRES_GET_ERROR");
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorCodeFromMessageAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0005"));
    }

    [Fact]
    public async Task IgnoresAMessageReadOutsideAnAxisErrorFactoryAsync()
    {
        const string source =
            """
            using System;
            class C
            {
                string M(Exception ex) => ex.Message;
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorCodeFromMessageAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0005"));
    }
}
