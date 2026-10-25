namespace Scaffolds.ECommerce.E2ETests.Fixtures;

internal static class TestData
{
    public static string NewSku(string prefix = "SKU") => $"{prefix}-{Guid.NewGuid():N}";
}
