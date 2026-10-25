namespace Axis.Conventions.Analyzers.UnitTests;

public class DomainPropertiesRecordAccessibilityAnalyzerTests
{
    private const string Prelude =
        """
        public interface IProductEntityProperties { string Name { get; } }

        """;

    [Fact]
    public async Task FlagsPublicPropertiesRecordAsync()
    {
        var source = Prelude +
                     """
                     public sealed record ProductProperties(string Name) : IProductEntityProperties;
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<DomainPropertiesRecordAccessibilityAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0604"));
    }

    [Fact]
    public async Task FlagsNonSealedInternalPropertiesRecordAsync()
    {
        var source = Prelude +
                     """
                     internal record ProductProperties(string Name) : IProductEntityProperties;
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<DomainPropertiesRecordAccessibilityAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0604"));
    }

    [Fact]
    public async Task IgnoresInternalSealedPropertiesRecordAsync()
    {
        var source = Prelude +
                     """
                     internal sealed record ProductProperties(string Name) : IProductEntityProperties;
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<DomainPropertiesRecordAccessibilityAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0604"));
    }

    [Fact]
    public async Task IgnoresRecordThatImplementsNoPropertiesInterfaceAsync()
    {
        var source =
            """
            public sealed record GetProductResponse(string ProductId, string Name);
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DomainPropertiesRecordAccessibilityAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0604"));
    }

    [Fact]
    public async Task IgnoresInterfaceWhoseNameDoesNotEndInPropertiesAsync()
    {
        var source =
            """
            public interface IProductThing { string Name { get; } }
            public sealed record ProductThing(string Name) : IProductThing;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<DomainPropertiesRecordAccessibilityAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0604"));
    }

    [Fact]
    public async Task IgnoresInternalNonSealedEntityWithProtectedMemberAsync()
    {
        // The AggregateApplication subclass lives in a downstream Application-project assembly the analyzer
        // never sees; the decidable signal, from this compilation alone, is the protected Rules member itself.
        var source = Prelude +
                     """
                     internal partial class ProductEntity(string Name) : IProductEntityProperties
                     {
                         public string Name { get; } = Name;

                         protected bool EnsureNameNotEmpty() => Name.Length > 0;
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<DomainPropertiesRecordAccessibilityAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0604"));
    }

    [Fact]
    public async Task FlagsInternalNonSealedEntityWithNoProtectedMemberAsync()
    {
        var source = Prelude +
                     """
                     internal partial class ProductEntity(string Name) : IProductEntityProperties
                     {
                         public string Name { get; } = Name;
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<DomainPropertiesRecordAccessibilityAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0604"));
    }
}
