using Axis.Analyzers;

namespace AxisMediator.Contracts.Analyzers.UnitTests;

public class PipelineContextLiteralKeyAnalyzerTests
{
    [Fact]
    public async Task FlagsAStringLiteralKeyOnSetAsync()
    {
        const string source =
            """
            using AxisMediator.Contracts.Pipelines;

            class Behavior
            {
                public void Use(AxisPipelineContext context) => context.Set("my.key", 42);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PipelineContextLiteralKeyAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0403"));
    }

    [Fact]
    public async Task IgnoresAKeyReadFromAKeyConstantAsync()
    {
        const string source =
            """
            using AxisMediator.Contracts.Pipelines;

            class Behavior
            {
                public int Use(AxisPipelineContext context) => context.Get<int>(AxisPipelineContextKeys.Span);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PipelineContextLiteralKeyAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0403"));
    }

    [Fact]
    public async Task IgnoresASetMethodOnAnUnrelatedTypeAsync()
    {
        const string source =
            """
            class Other
            {
                public void Set(string key, int value) { }
                public void Use() => Set("x", 1);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PipelineContextLiteralKeyAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0403"));
    }
}
