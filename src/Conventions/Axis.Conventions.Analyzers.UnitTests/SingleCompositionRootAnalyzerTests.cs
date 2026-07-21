namespace Axis.Conventions.Analyzers.UnitTests;

public class SingleCompositionRootAnalyzerTests
{
    [Fact]
    public async Task IgnoresSinglePublicDependencyInjectionAsync()
    {
        var source =
            """
            namespace Sample.Root;
            public static class DependencyInjection { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<SingleCompositionRootAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0607"));
    }

    [Fact]
    public async Task IgnoresInternalPerFeatureDependencyInjectionAsync()
    {
        var source =
            """
            namespace Sample.Root
            {
                public static class DependencyInjection { }
            }

            namespace Sample.Root.Catalog
            {
                internal static class DependencyInjection { }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<SingleCompositionRootAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0607"));
    }

    [Fact]
    public async Task FlagsMultiplePublicDependencyInjectionAsync()
    {
        var source =
            """
            namespace Sample.Root
            {
                public static class DependencyInjection { }
            }

            namespace Sample.Root.Catalog
            {
                public static class DependencyInjection { }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<SingleCompositionRootAnalyzer>(source);

        Assert.Equal(2, diagnostics.Count("AXIS0607"));
    }

    [Fact]
    public async Task IgnoresUnrelatedPublicClassNamedDifferentlyAsync()
    {
        var source =
            """
            namespace Sample.Root;
            public static class NotComposition { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<SingleCompositionRootAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0607"));
    }
}
