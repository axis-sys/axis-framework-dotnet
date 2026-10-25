namespace Axis.Conventions.Analyzers.UnitTests;

public class ControllerSealedAnalyzerTests
{
    [Fact]
    public async Task FlagsTheClassWhenAControllerIsNotSealedAsync()
    {
        const string source =
            """
            using Microsoft.AspNetCore.Mvc;
            public class WidgetsController : ControllerBase { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ControllerSealedAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0602"));
    }

    [Fact]
    public async Task FlagsTheClassWhenAViewControllerIsNotSealedAsync()
    {
        const string source =
            """
            using Microsoft.AspNetCore.Mvc;
            public class WidgetsController : Controller { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ControllerSealedAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0602"));
    }

    [Fact]
    public async Task FlagsNothingWhenTheControllerIsSealedAsync()
    {
        const string source =
            """
            using Microsoft.AspNetCore.Mvc;
            public sealed class WidgetsController : ControllerBase { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ControllerSealedAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0602"));
    }

    [Fact]
    public async Task IgnoresTheClassWhenTheControllerIsAnAbstractBaseAsync()
    {
        const string source =
            """
            using Microsoft.AspNetCore.Mvc;
            public abstract class ApiControllerBase : ControllerBase { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ControllerSealedAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0602"));
    }

    [Fact]
    public async Task IgnoresTheClassWhenItIsNotAControllerAsync()
    {
        const string source =
            """
            public class NotAController { }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<ControllerSealedAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0602"));
    }
}
