namespace Axis.Conventions.Analyzers.UnitTests;

public class TestMethodNamingAnalyzerTests
{
    private const string FactDef =
        """
        using System.Threading.Tasks;
        public sealed class FactAttribute : System.Attribute { }

        """;

    [Fact]
    public async Task FlagsSnakeCaseTestNameAsync()
    {
        var source = FactDef +
                     """
                     public class T
                     {
                         [Fact] public void Creates_the_thing() { }
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<TestMethodNamingAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0603"));
    }

    [Fact]
    public async Task FlagsAsyncTestWithoutAsyncSuffixAsync()
    {
        var source = FactDef +
                     """
                     public class T
                     {
                         [Fact] public async Task DoesTheThing() { await Task.CompletedTask; }
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<TestMethodNamingAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0603"));
    }

    [Fact]
    public async Task IgnoresPascalCaseTestsAsync()
    {
        var source = FactDef +
                     """
                     public class T
                     {
                         [Fact] public async Task DoesTheThingAsync() { await Task.CompletedTask; }
                         [Fact] public void ComputesTheThing() { }
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<TestMethodNamingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0603"));
    }

    [Fact]
    public async Task IgnoresNonTestMethodAsync()
    {
        var source = FactDef +
                     """
                     public class T
                     {
                         public void Helps_with_things() { }
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<TestMethodNamingAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0603"));
    }
}
