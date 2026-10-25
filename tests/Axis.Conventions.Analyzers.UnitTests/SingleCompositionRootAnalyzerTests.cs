namespace Axis.Conventions.Analyzers.UnitTests;

public class SingleCompositionRootAnalyzerTests
{
    [Fact]
    public async Task FlagsNothingWhenTheAssemblyHasASinglePublicDependencyInjectionAsync()
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
    public async Task FlagsNothingWhenThePerFeatureDependencyInjectionIsInternalAsync()
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
    public async Task FlagsEveryClassWhenTheAssemblyHasTwoPublicDependencyInjectionClassesAsync()
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
    public async Task IgnoresTheClassWhenItIsNotNamedDependencyInjectionAsync()
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
