namespace Axis.Conventions.Analyzers.UnitTests;

public class ValidatorAccessModifierAnalyzerTests
{
    private const string Prelude =
        """
        using AxisValidator;
        public sealed class Cmd { }

        """;

    [Fact]
    public async Task FlagsPublicValidatorAsync()
    {
        var source = Prelude +
                     """
                     public sealed class CmdValidator : AxisValidatorBase<Cmd> { }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValidatorAccessModifierAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0601"));
    }

    [Fact]
    public async Task FlagsNonSealedValidatorAsync()
    {
        var source = Prelude +
                     """
                     internal class CmdValidator : AxisValidatorBase<Cmd> { }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValidatorAccessModifierAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0601"));
    }

    [Fact]
    public async Task IgnoresInternalSealedValidatorAsync()
    {
        var source = Prelude +
                     """
                     internal sealed class CmdValidator : AxisValidatorBase<Cmd> { }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValidatorAccessModifierAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0601"));
    }

    [Fact]
    public async Task IgnoresNonValidatorClassAsync()
    {
        var source = Prelude +
                     """
                     public class NotAValidator { }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ValidatorAccessModifierAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0601"));
    }
}
