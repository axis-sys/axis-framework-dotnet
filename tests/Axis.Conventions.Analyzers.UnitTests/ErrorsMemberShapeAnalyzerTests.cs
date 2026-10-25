namespace Axis.Conventions.Analyzers.UnitTests;

public class ErrorsMemberShapeAnalyzerTests
{
    private const string Prelude =
        """
        using Axis;

        """;

    [Fact]
    public async Task FlagsTheCallWhenAnErrorsClassConstIsBuiltIntoAnAxisErrorAtTheCallSiteAsync()
    {
        var source = Prelude +
                     """
                     internal static class PageStorageErrors
                     {
                         public const string PAGE_NOT_CONFIGURED = "PAGE_NOT_CONFIGURED";
                     }

                     public static class NoPageStorage
                     {
                         public static AxisError Upload() => AxisError.InternalServerError(PageStorageErrors.PAGE_NOT_CONFIGURED);
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorsMemberShapeAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0608"));
    }

    [Fact]
    public async Task FlagsEveryCallSiteWhenTheSameCodeIsRebuiltRepeatedlyAsync()
    {
        var source = Prelude +
                     """
                     internal static class PageStorageErrors
                     {
                         public const string PAGE_NOT_CONFIGURED = "PAGE_NOT_CONFIGURED";
                     }

                     public static class NoPageStorage
                     {
                         public static AxisError Upload() => AxisError.InternalServerError(PageStorageErrors.PAGE_NOT_CONFIGURED);
                         public static AxisError Download() => AxisError.InternalServerError(PageStorageErrors.PAGE_NOT_CONFIGURED);
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorsMemberShapeAnalyzer>(source);

        Assert.Equal(2, diagnostics.Count("AXIS0608"));
    }

    [Fact]
    public async Task IgnoresTheCallWhenItIsInsideTheErrorsClassItselfAsync()
    {
        var source = Prelude +
                     """
                     internal static class PageStorageErrors
                     {
                         private const string PAGE_NOT_CONFIGURED = "PAGE_NOT_CONFIGURED";

                         public static readonly AxisError PageNotConfigured = AxisError.InternalServerError(PAGE_NOT_CONFIGURED);
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorsMemberShapeAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0608"));
    }

    [Fact]
    public async Task FlagsNothingWhenTheCallSiteReturnsTheReadyMemberAsync()
    {
        var source = Prelude +
                     """
                     internal static class PageStorageErrors
                     {
                         public static readonly AxisError PageNotConfigured = AxisError.InternalServerError("PAGE_NOT_CONFIGURED");
                     }

                     public static class NoPageStorage
                     {
                         public static AxisError Upload() => PageStorageErrors.PageNotConfigured;
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorsMemberShapeAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0608"));
    }

    [Fact]
    public async Task IgnoresTheConstWhenItIsConsumedAsARawValidationCodeAsync()
    {
        var source = Prelude +
                     """
                     internal static class ProductErrors
                     {
                         public const string CREATE_PRODUCT_NAME_REQUIRED = "CREATE_PRODUCT_NAME_REQUIRED";
                     }

                     public static class Consumer
                     {
                         public static string Code() => ProductErrors.CREATE_PRODUCT_NAME_REQUIRED;
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorsMemberShapeAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0608"));
    }

    [Fact]
    public async Task IgnoresTheConstWhenItDoesNotComeFromAnErrorsClassAsync()
    {
        var source = Prelude +
                     """
                     internal static class RecordStatusCodes
                     {
                         public const string PENDING = "PENDING";
                     }

                     public static class Consumer
                     {
                         public static AxisError Build() => AxisError.Conflict(RecordStatusCodes.PENDING);
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorsMemberShapeAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0608"));
    }

    [Fact]
    public async Task IgnoresTheCallWhenTheCodeIsComposedFromRuntimeContextAsync()
    {
        var source = Prelude +
                     """
                     internal static class TenantErrors
                     {
                         public static AxisError NotFound(string tenantId) => AxisError.NotFound($"TENANT_NOT_FOUND_{tenantId}");
                     }

                     public static class Consumer
                     {
                         public static AxisError Build(string tenantId) => TenantErrors.NotFound(tenantId);
                     }
                     """;

        var diagnostics = await AnalyzerHarness.RunAsync<ErrorsMemberShapeAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0608"));
    }
}
