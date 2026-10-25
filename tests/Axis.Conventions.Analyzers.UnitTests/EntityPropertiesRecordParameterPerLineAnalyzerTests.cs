namespace Axis.Conventions.Analyzers.UnitTests;

public class EntityPropertiesRecordParameterPerLineAnalyzerTests
{
    [Fact]
    public async Task FlagsTheRecordWhenAllParametersShareTheDeclarationLineAsync()
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
    public async Task FlagsTheRecordWhenTwoParametersShareALineAsync()
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
    public async Task FlagsTheRecordWhenTheFirstParameterSharesTheDeclarationLineAsync()
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
    public async Task FlagsNothingWhenEachParameterIsOnItsOwnLineAsync()
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
    public async Task IgnoresTheRecordWhenItHasASingleParameterAsync()
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
    public async Task IgnoresTheRecordWhenItImplementsNoPropertiesInterfaceAsync()
    {
        const string source =
            """
            public sealed record GetProductResponse(string ProductId, string Name);
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<EntityPropertiesRecordParameterPerLineAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0606"));
    }

    [Fact]
    public async Task IgnoresTheRecordWhenTheInterfaceNameDoesNotEndInPropertiesAsync()
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
    public async Task IgnoresTheRecordWhenItIsARecordStructAsync()
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
