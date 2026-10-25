namespace Axis.Conventions.Analyzers.UnitTests;

public class EntityPropertiesRecordParameterPerLineAnalyzerTests
{
    [Fact]
    public async Task FlagsSingleLineMultiParameterRecordAsync()
    {
        const string source =
            """
            public interface IThingProperties { string A { get; } string B { get; } }
            internal sealed record Thing(string A, string B) : IThingProperties;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task FlagsWhenTwoParametersShareALineAsync()
    {
        const string source =
            """
            public interface IThingProperties { string A { get; } string B { get; } }
            internal sealed record Thing(
                string A, string B
            ) : IThingProperties;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task FlagsWhenFirstParameterSharesTheDeclarationLineAsync()
    {
        const string source =
            """
            public interface IThingProperties { string A { get; } string B { get; } }
            internal sealed record Thing(string A,
                string B
            ) : IThingProperties;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task IgnoresOneParameterPerLineRecordAsync()
    {
        const string source =
            """
            public interface IThingProperties { string A { get; } string B { get; } }
            internal sealed record Thing(
                string A,
                string B
            ) : IThingProperties;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task IgnoresSingleParameterRecordAsync()
    {
        const string source =
            """
            public interface IThingProperties { string A { get; } }
            internal sealed record Thing(string A) : IThingProperties;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task IgnoresRecordImplementingNoPropertiesInterfaceAsync()
    {
        const string source =
            """
            public sealed record GetProductResponse(string ProductId, string Name);
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task IgnoresInterfaceWhoseNameDoesNotEndInPropertiesAsync()
    {
        const string source =
            """
            public interface IThing { string A { get; } string B { get; } }
            public sealed record Thing(string A, string B) : IThing;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task IgnoresRecordStructValueObjectAsync()
    {
        // A record struct is a value object, not an entity — SyntaxKind.RecordStructDeclaration is out of scope.
        const string source =
            """
            public interface IPointProperties { int X { get; } int Y { get; } }
            public readonly record struct Point(int X, int Y) : IPointProperties;
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0606"));
    }
}
