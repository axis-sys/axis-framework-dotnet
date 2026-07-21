using Axis.Analyzers;

namespace AxisTypes.Analyzers.UnitTests;

public class ValueObjectReadonlyAnalyzerTests
{
    private const string Attribute =
        "namespace AxisTypes.SourceGenerator { class ValueObjectAttribute : System.Attribute {} }";

    [Fact]
    public async Task FlagsNonReadonlyValueObjectStructAsync()
    {
        const string source = Attribute + """

            [AxisTypes.SourceGenerator.ValueObject]
            partial struct Sku { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValueObjectReadonlyAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0200"));
    }

    [Fact]
    public async Task IgnoresReadonlyValueObjectStructAsync()
    {
        const string source = Attribute + """

            [AxisTypes.SourceGenerator.ValueObject]
            readonly partial struct Sku { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValueObjectReadonlyAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0200"));
    }

    [Fact]
    public async Task IgnoresValueObjectReferenceTypeAsync()
    {
        const string source = Attribute + """

            [AxisTypes.SourceGenerator.ValueObject]
            partial class Ref { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValueObjectReadonlyAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0200"));
    }

    [Fact]
    public async Task IgnoresPlainStructWithoutTheAttributeAsync()
    {
        const string source = Attribute + """

            partial struct Plain { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValueObjectReadonlyAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0200"));
    }
}
